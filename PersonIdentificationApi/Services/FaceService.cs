using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using Microsoft.Extensions.Options;
using PersonIdentificationApi.Helpers;
using PersonIdentificationApi.Models;
using PersonIdentificationApi.Services;
using PersonIdentificationApi.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace PersonIdentification.FaceService
{
    public class FaceService : IFaceService
    {
        private readonly FaceSettings _faceSettings;
        const string RECOGNITION_MODEL4 = RecognitionModel.Recognition04;
        private readonly ILogger<FaceService> _logger;
        private readonly PersonGroupRepository _personGroupRepository;
        private readonly BlobUtility _blobUtility;

        public FaceService(
            IOptions<FaceSettings> faceSettings,
            ILogger<FaceService> logger,
            PersonGroupRepository personGroupRepository)
        {
            _faceSettings = faceSettings.Value;
            _logger = logger;
            _personGroupRepository = personGroupRepository;

            _blobUtility = new BlobUtility
            {
                ConnectionString = Helper.GetEnvironmentVariable("BlobConnectionString"),
                ContainerName = Helper.GetEnvironmentVariable("ContainerName")
            };
        }


        // This is an example of identification using Azure AI Face service.
        // Assumes that a set of images have been trained and a person group has been created.
        // Person group and images trained are stored in the database. Images trained are stored in Azure Blob Storage.
        public async Task<List<DetectedFaceResponse>> DetectFaceRecognize(List<string> imagesToIdentify)
        {
            var detectedFaceResponse = new List<DetectedFaceResponse>();

            // Get all trained groups for lookup
            var personGroupImages = await _personGroupRepository.GetPersonGroupsAsync();
            var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSettings.SubscriptionKey)) { Endpoint = _faceSettings.EndPoint };

            // Loop through all passed in images that need to be identified
            // Get all trained groups for lookup
            foreach (var imageToIdentify in imagesToIdentify)
            {
                foreach (var personGroupImage in personGroupImages)
                {
                    var sourceFaceIds = new List<Guid>();

                    // Detect faces from source image url.
                    var sasUri = _blobUtility.GetBlobSasUri(imageToIdentify);

                    var detectedFaces = await DetectFaceRecognize(faceClient, sasUri.ToString());

                    // Add detected faceId to sourceFaceIds.
                    foreach (var detectedFace in detectedFaces) { sourceFaceIds.Add(detectedFace.FaceId.Value); }

                    // Identify the faces in a person group. 
                    var identifyResults = await faceClient.Face.IdentifyAsync(sourceFaceIds, personGroupImage.PersonGroupId.ToString());

                    foreach (IdentifyResult? identifyResult in identifyResults)
                    {
                        var verifyResults = new List<VerifyResult>();
                        var facesIdentified = new List<FacesIdentified>();

                        if (!identifyResult.Candidates.Any())
                        {
                            _logger.LogInformation($"No person is identified for the face in: {imageToIdentify} - {identifyResult.FaceId},");

                            continue;
                        }

                        foreach (var candidate in identifyResult.Candidates)
                        {
                            var person = await faceClient.PersonGroupPerson.GetAsync(personGroupImage.PersonGroupId.ToString(), candidate.PersonId);

                            if (person != null)
                            {
                                var response = new DetectedFaceResponse();

                                _logger.LogInformation($"Person '{person.Name}' is identified for the face in: {imageToIdentify} - {identifyResult.FaceId}, confidence: {candidate.Confidence}.");

                                var verifyResult = await faceClient.Face.VerifyFaceToPersonAsync(identifyResult.FaceId, person.PersonId, personGroupImage.PersonGroupId.ToString());

                                response.PersonGroupId = personGroupImage.PersonGroupId.ToString();
                                response.PersonId = person.PersonId.ToString();
                                response.ImageToIdentify = imageToIdentify;
                                response.BlobName = personGroupImage.BlobName;
                                response.BlobUrl = personGroupImage.BlobUrl;

                                var faceIdentified = new FacesIdentified
                                {
                                    FaceId = identifyResult.FaceId.ToString(),
                                    VerifyResults = verifyResult
                                };

                                facesIdentified.Add(faceIdentified);
                                response.FacesIdentified = facesIdentified;                                
                                detectedFaceResponse.Add(response);

                                _logger.LogInformation($"Verification result: is a match? {verifyResult.IsIdentical}. confidence: {verifyResult.Confidence}");
                            }
                        }
                    }
                }
            }

            return detectedFaceResponse;
        }

        // This is an example of how to train images using Azure AI Face service.
        // Stores the person group and images trained in the database.
        public async Task<string> TrainAsync(List<string> imageSasUrls)
        {
            var personGroupId = Guid.NewGuid().ToString();
            var personId = Guid.NewGuid();

            _logger.LogInformation($"Create a person group ({personGroupId}).");

            using var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSettings.SubscriptionKey)) { Endpoint = _faceSettings.EndPoint };

            // Create a Person Group
            await faceClient.PersonGroup.CreateAsync(personGroupId, $"Group: {personGroupId}", recognitionModel: RECOGNITION_MODEL4);

            var personGroupClient = faceClient.PersonGroupPerson;
            bool sufficientQuality;
            var imageName = string.Empty;
            Uri? sasUri = null;

            foreach (var sasUrl in imageSasUrls)
            {
                sasUri = new Uri(sasUrl);
                imageName = Path.GetFileName(sasUri.LocalPath);

                // Download the blob's contents to a memory stream
                byte[] blobContent = await _blobUtility.DownloadBlobStreamAsync(sasUrl);

                IList<DetectedFace> detectedFaces;
                using (var detectionStream = new MemoryStream(blobContent))
                {
                    detectedFaces = await faceClient.Face.DetectWithStreamAsync(detectionStream,
                        recognitionModel: RECOGNITION_MODEL4,
                        detectionModel: DetectionModel.Detection03,
                        returnFaceAttributes: new List<FaceAttributeType> { FaceAttributeType.QualityForRecognition });
                }

                if (detectedFaces.Any())
                {
                    if (detectedFaces.Count > 1)
                    {
                        foreach (var detectedFace in detectedFaces)
                        {
                            sufficientQuality = IsQualityForRecognition(detectedFace);

                            if (sufficientQuality)
                            {
                                _logger.LogInformation($"Quality sufficient for recognition for detected face. FaceId: {detectedFace.FaceId} Image: {imageName}");
                                var person = await faceClient.PersonGroupPerson.CreateAsync(personGroupId, detectedFace.FaceId.ToString());

                                // Crop the image to only include the detected face
                                var faceRectangle = detectedFace.FaceRectangle;
                                var croppedImage = CropImage(blobContent, faceRectangle);

                                using var addFaceStream = new MemoryStream(croppedImage);
                                await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(personGroupId, person.PersonId, addFaceStream, detectedFace.FaceId.ToString());
                            }
                            else
                            {
                                _logger.LogInformation($"Insuffucient quality for recognition for detected face. FaceId: {detectedFace.FaceId} Image: {imageName}");
                            }
                        }
                    }
                    else
                    {
                        var detectedFace = detectedFaces.FirstOrDefault();

                        if (detectedFace != null) // this seems unlikely to be null, but just in case
                        {
                            sufficientQuality = IsQualityForRecognition(detectedFace);

                            if (sufficientQuality)
                            {
                                _logger.LogInformation($"Quality sufficient for recognition for detected face. FaceId: {detectedFace.FaceId} Image: {imageName}");
                                var person = await faceClient.PersonGroupPerson.CreateAsync(personGroupId, detectedFace.FaceId.ToString());

                                // No need to crop the image as it contains only one face
                                using var addFaceStream = new MemoryStream(blobContent);
                                await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(personGroupId, person.PersonId, addFaceStream, detectedFace.FaceId.ToString());
                            }
                            else
                            {
                                _logger.LogInformation($"Insuffucient quality for recognition for detected face. FaceId: {detectedFace.FaceId} Image: {imageName}");
                            }
                        }
                    }
                }
            }

            // TODO: This should be done in a background process
            await TrainAndCheckStatusAsync(imageSasUrls, personGroupId, personId, faceClient);

            return personGroupId;
        }

        public async Task<string> GetTrainingStatusAsync(string personGroupId)
        {
            using var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSettings.SubscriptionKey)) { Endpoint = _faceSettings.EndPoint };

            var trainingStatus = await faceClient.PersonGroup.GetTrainingStatusAsync(personGroupId);

            return trainingStatus.Status.ToString();
        }

        // Detect faces from image url for recognition purposes. This is a helper method for other functions in this quickstart.
        // Parameter `returnFaceId` of `DetectWithUrlAsync` must be set to `true` (by default) for recognition purposes.
        // Parameter `FaceAttributes` is set to include the QualityForRecognition attribute. 
        // Recognition model must be set to recognition_03 or recognition_04 as a result.
        // Result faces with insufficient quality for recognition are filtered out. 
        // The field `faceId` in returned `DetectedFace`s will be used in Face - Face - Verify and Face - Identify.
        // It will expire 24 hours after the detection call.
        private async Task<List<DetectedFace>> DetectFaceRecognize(IFaceClient faceClient, string sasUrl)
        {
            // Detect faces from image URL. Since only recognizing, use the recognition model 1.
            // We use detection model 3 because we are not retrieving attributes.
            var detectedFaces = await faceClient.Face.DetectWithUrlAsync(sasUrl, recognitionModel: RECOGNITION_MODEL4, detectionModel: DetectionModel.Detection03, returnFaceAttributes: new List<FaceAttributeType> { FaceAttributeType.QualityForRecognition });
            var sasUri = new Uri(sasUrl);
            var imageName = Path.GetFileName(sasUri.LocalPath);
            var sufficientQualityFaces = new List<DetectedFace>();
            
            foreach (var detectedFace in detectedFaces)
            {
                var faceQualityForRecognition = IsQualityForRecognition(detectedFace);

                if (faceQualityForRecognition)
                {
                    _logger.LogInformation($"Quality sufficient for recognition for detected face. FaceId: {detectedFace.FaceId} Image: {imageName}");
                    sufficientQualityFaces.Add(detectedFace);
                }
                else
                {
                     _logger.LogInformation($"Insuffucient quality for recognition for detected face. FaceId: {detectedFace.FaceId} Image: {imageName}");
                }
            }

            _logger.LogInformation($"{detectedFaces.Count} face(s) with {sufficientQualityFaces.Count} having sufficient quality for recognition detected from image `{imageName}`");

            return sufficientQualityFaces.ToList();
        }

        private static bool IsQualityForRecognition(DetectedFace detectedFace)
        {
            bool sufficientQuality = true;
            var faceQualityForRecognition = detectedFace.FaceAttributes.QualityForRecognition;

            //  Only "high" quality images are recommended for person enrollment
            if (faceQualityForRecognition.HasValue && (faceQualityForRecognition.Value != QualityForRecognition.High))
            {
                sufficientQuality = false;
            }

            return sufficientQuality;
        }

        private static byte[] CropImage(byte[] imageBytes, FaceRectangle faceRectangle)
        {
            using var ms = new MemoryStream(imageBytes);
            var image = SixLabors.ImageSharp.Image.Load(ms);

            var cropArea = new Rectangle(faceRectangle.Left, faceRectangle.Top, faceRectangle.Width, faceRectangle.Height);
            var croppedImage = image.Clone(ctx => ctx.Crop(cropArea));
            
            using var croppedMs = new MemoryStream();
            croppedImage.SaveAsJpeg(croppedMs);
            return croppedMs.ToArray();
        }

        private async Task SavePersonGroupToDb(string personGroupId, List<string> sasUrls)
        {
           // Save the person group to the database
            var personGroup = new DbPersonGroup
            {
                PersonGroupId = Guid.Parse(personGroupId),
                IsTrained = true,
                IsDeleted = false,
                CreatedBy = "System"
            };

            await _personGroupRepository.InsertPersonGroupAsync(personGroup);

            foreach (var sasUrl in sasUrls)
            {
                var sasUri = new Uri(sasUrl);
                var blobUrl = sasUri.GetLeftPart(UriPartial.Path);
                var blobName = Path.GetFileName(sasUri.LocalPath);

                var personGroupImage = new DbPersonGroupImage
                {
                    PersonGroupImageId = Guid.NewGuid(),
                    PersonGroupId = Guid.Parse(personGroupId),
                    BlobName = blobName,
                    BlobUrl = blobUrl,
                    CreatedBy = "System"
                };

                await _personGroupRepository.InsertPersonGroupImageAsync(personGroupImage);
            }
        }

        private async Task TrainAndCheckStatusAsync(List<string> imageSasUrls, string personGroupId, Guid personId, FaceClient faceClient)
        {
            // TODO: This should be done in a background process
            await faceClient.PersonGroup.TrainAsync(personGroupId);

            // TODO: This should be done in a background process
            while (true)
            {
                await Task.Delay(1000);
                var trainingStatus = await faceClient.PersonGroup.GetTrainingStatusAsync(personGroupId);

                _logger.LogInformation($"Training status: {trainingStatus.Status}. Message: {trainingStatus.Message}");

                if (trainingStatus.Status == TrainingStatusType.Succeeded)
                {
                    await SavePersonGroupToDb(personGroupId, imageSasUrls);
                    break;
                }

                if (trainingStatus.Status == TrainingStatusType.Failed)
                {
                    throw new Exception("Training failed");
                }
            }
        }
    }
}