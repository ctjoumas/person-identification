using PersonIdentificationApi.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace PersonIdentificationApi.Services
{
    public class Segmentation : ISegmentation
    {
        private readonly ILogger<Segmentation> _logger;
        private readonly IConfiguration _configuration;
        private readonly BlobUtility _blobUtility;
        private readonly string _mlKey;
        private readonly string _mlEndpoint;

        public Segmentation(ILogger<Segmentation> logger, IConfiguration configuration)
        {
            _logger = logger;

            _configuration = configuration;

            _blobUtility = new BlobUtility
            {
                ConnectionString = _configuration.GetValue<string>("BlobConnectionString"),
                ContainerName = _configuration.GetValue<string>("ContainerName")
            };

            if (string.IsNullOrWhiteSpace(_blobUtility.ConnectionString) || string.IsNullOrWhiteSpace(_blobUtility.ContainerName))
            {
                throw new Exception("BlobConnectionString and ContainerName are required.");
            }

            _mlKey = _configuration.GetValue<string>("MLKey");
            _mlEndpoint = _configuration.GetValue<string>("MLEndpoint");

            if (string.IsNullOrEmpty(_mlKey) || string.IsNullOrWhiteSpace(_mlEndpoint))
            {
                throw new Exception("A key and endpoint should be provided to invoke the Azure Machine Learning Model.");
            }
        }

        public async Task<List<string>> RunSegmentation(string fileName, string imageUrl)
        {
            List<string> segmentImages = new List<string>();

            // gets the byte array of the segment / image

            byte[] imageByteStream = _blobUtility.DownloadBlobStreamAsync(imageUrl).Result;

            if (imageByteStream != null)
            {
                string base64ImageRepresentation = Convert.ToBase64String(imageByteStream);

                HttpResponseMessage response = CallMlSegmentationModel(base64ImageRepresentation).Result;

                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    var segmentationModels = JsonSerializer.Deserialize<SegmentationModel[]>(result);

                    // get the width and height of the source image and use this to create each segment image
                    // this is possibly not needed and we can create a standard width/height
                    int x, y;
                    using (var imageStream = new MemoryStream(imageByteStream))
                    using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageStream))
                    {
                        x = image.Size.Width;
                        y = image.Size.Height;
                    }

                    int segmentNumber = 1;

                    // there is only one element in the array, so start from there
                    var segmentationModel = segmentationModels[0];
                    foreach (var box in segmentationModel.Boxes)
                    {
                        // only process this segment if it's a person that is identified
                        if (box.Label.TrimEnd('\n').ToLower().Equals("person") && box.Score > 0.9)
                        {
                            List<PointF> pointFs = new List<PointF>();

                            // push the list of coordinates in the polygon to pairs of (x, y) points
                            for (int i = 0; i < box.Polygon[0].Count - 1; i += 2)
                            {
                                //PointF point = new PointF(box.Polygon[0][i], box.Polygon[0][i + 1]);
                                PointF point = new PointF(box.Polygon[0][i] * x, box.Polygon[0][i + 1] * y);
                                pointFs.Add(point);
                            }

                            using (var inStream = new MemoryStream(imageByteStream))
                            // uncomment this if you want to see the dashed outline of this segment on the original image
                            //using (var outStream = File.Create("C:\\Code With\\NBA\\test.png"))
                            // uncomment this if you want to see the segment saved as a separate image (extracted from a group of segments in the original image)
                            //using (var outStream2 = File.Create("C:\\Code With\\NBA\\polygon.png"))
                            using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(inStream))
                            {
                                var pathBuilder = new SixLabors.ImageSharp.Drawing.PathBuilder();
                                pathBuilder.AddLines(pointFs.ToArray());
                                var path = pathBuilder.Build();

                                int polygonWidth = (int)(box.Box.BottomX * x - box.Box.TopX * x);
                                int polygonHeight = (int)(box.Box.BottomY * y - box.Box.TopY * y);
                                Image<Rgba32> newImage = new Image<Rgba32>(polygonWidth, polygonHeight);
                                SixLabors.ImageSharp.Drawing.Polygon polygon = new SixLabors.ImageSharp.Drawing.Polygon(pointFs.ToArray());

                                MemoryStream polygonStream = new MemoryStream();
                                // crop the rectangular bounds of the polygon from the image and save it to the stream
                                newImage = image.Clone(c => c.Crop((Rectangle)polygon.Bounds));
                                newImage.Save(polygonStream, new PngEncoder());

                                // upload this segment to the storage account container and save the name to the list so it
                                // can be later passed to the face identification service
                                int lastDotIndex = fileName.LastIndexOf('.');
                                string segmentFileName = fileName.Insert(lastDotIndex, $"_{segmentNumber}");
                                await _blobUtility.UploadFromStreamAsync(polygonStream, segmentFileName);

                                segmentImages.Add(segmentFileName);

                                // uncomment this line to save the segment image to local disk
                                //newImage.Save(outStream2, new JpegEncoder());

                                // uncomment out these lines if you want to save and view the segment
                                //image.Mutate(x => x.DrawPolygon(Pens.DashDotDot(Color.Red, 5), pointFs.ToArray()));
                                //image.Save(outStream, new JpegEncoder());
                            }

                            _logger.LogInformation(box.Label);
                            _logger.LogInformation(box.Score.ToString());
                            _logger.LogInformation(box.Box.ToString());
                            _logger.LogInformation(box.Polygon.ToString());

                            segmentNumber++;
                        }
                    }

                    _logger.LogInformation("Result: {0}", result);
                }
                else
                {
                    _logger.LogInformation(string.Format("The request failed with status code: {0}", response.StatusCode));

                    // Print the headers - they include the requert ID and the timestamp,
                    // which are useful for debugging the failure
                    _logger.LogInformation(response.Headers.ToString());

                    string responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation(responseContent);
                }
            }

            return segmentImages;
        }

        /// <summary>
        /// Helper method to call the segmentation ML model.
        /// </summary>
        /// <param name="base64ImageRepresentation">The base64 representation of the source image we are segmenting</param>
        /// <returns>The segmentation results</returns>
        /// <exception cref="Exception"></exception>
        private async Task<HttpResponseMessage> CallMlSegmentationModel(string base64ImageRepresentation)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            var handler = new HttpClientHandler()
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback =
                        (httpRequestMessage, cert, cetChain, policyErrors) => { return true; }
            };

            using (var client = new HttpClient(handler))
            {
                // create the request payload for the ML Model
                var requestBody = @"{
                    ""input_data"": {
                    ""columns"": [
                        ""image""
                    ],
                    ""index"": [0],
                    ""data"": [""" + base64ImageRepresentation + @"""]
                    },
                    ""params"": {}
                }";

                // Replace this with the primary/secondary key, AMLToken, or Microsoft Entra ID token for the endpoint
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _mlKey);

                client.BaseAddress = new Uri(_mlEndpoint);

                var content = new StringContent(requestBody);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                // This header will force the request to go to a specific deployment.
                // Remove this line to have the request observe the endpoint traffic rules
                content.Headers.Add("azureml-model-deployment", "automl-image-instance-segment-4");

                // WARNING: The 'await' statement below can result in a deadlock
                // if you are calling this code from the UI thread of an ASP.Net application.
                // One way to address this would be to call ConfigureAwait(false)
                // so that the execution does not attempt to resume on the original context.
                // For instance, replace code such as:
                //      result = await DoSomeTask()
                // with the following:
                //      result = await DoSomeTask().ConfigureAwait(false)
                response = await client.PostAsync(_mlEndpoint, content);
            }

            return response;
        }
    }
}