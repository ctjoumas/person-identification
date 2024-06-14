using PersonIdentificationApi.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Drawing.Processing;

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

                    // Get the dimensions of the source image
                    int imageWidth, imageHeight;
                    using (var imageStream = new MemoryStream(imageByteStream))
                    using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageStream))
                    {
                        imageWidth = image.Size.Width;
                        imageHeight = image.Size.Height;
                    }

                    int segmentNumber = 1;

                    // Process each segment identified by the ML model
                    var segmentationModel = segmentationModels[0];
                    foreach (var box in segmentationModel.Boxes)
                    {
                        // Only process if the segment is identified as a person with a high confidence score
                        if (box.Label.TrimEnd('\n').ToLower().Equals("person") && box.Score > 0.9)
                        {
                            List<PointF> points = new List<PointF>();

                            // Convert polygon coordinates to image scale
                            for (int i = 0; i < box.Polygon[0].Count - 1; i += 2)
                            {
                                PointF point = new PointF(box.Polygon[0][i] * imageWidth, box.Polygon[0][i + 1] * imageHeight);
                                points.Add(point);
                            }

                            using (var inStream = new MemoryStream(imageByteStream))
                            using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(inStream))
                            {
                                // Create a mask image
                                Image<Rgba32> mask = new Image<Rgba32>(imageWidth, imageHeight);
                                mask.Mutate(ctx =>
                                {
                                    ctx.Clear(Color.Transparent);
                                    var pathBuilder = new SixLabors.ImageSharp.Drawing.PathBuilder();
                                    pathBuilder.AddLines(points.ToArray());
                                    var path = pathBuilder.Build();
                                    ctx.Fill(Color.White, path);
                                });

                                // Apply the mask to the original image
                                image.Mutate(ctx =>
                                {
                                    ctx.DrawImage(mask, PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.DestIn, 1.0f);
                                });

                                // Calculate the bounding box of the polygon
                                var bounds = new SixLabors.ImageSharp.Drawing.PathBuilder()
                                                .AddLines(points.ToArray())
                                                .Build()
                                                .Bounds;

                                // Crop the image to the bounding box of the polygon
                                var croppedImage = image.Clone(ctx => ctx.Crop(new Rectangle((int)bounds.Left, (int)bounds.Top, (int)bounds.Width, (int)bounds.Height)));

                                // Create a new image with a white background
                                var finalImage = new Image<Rgba32>((int)bounds.Width, (int)bounds.Height);
                                finalImage.Mutate(ctx => ctx.Fill(Color.White));

                                // Draw the cropped image onto the new image with a white background
                                finalImage.Mutate(ctx => ctx.DrawImage(croppedImage, new Point(0, 0), 1.0f));

                                // Save the new image
                                using (var polygonStream = new MemoryStream())
                                {
                                    finalImage.Save(polygonStream, new PngEncoder());
                                    // You can use polygonStream to save the image to a desired location or return it as needed
                                    int lastDotIndex = fileName.LastIndexOf('.');
                                    string segmentFileName = fileName.Insert(lastDotIndex, $"_{segmentNumber}");
                                    await _blobUtility.UploadFromStreamAsync(polygonStream, segmentFileName);
                                }
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
