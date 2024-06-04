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

        public FaceService(
            IOptions<FaceSettings> faceSettings,
            ILogger<FaceService> logger,
            PersonGroupRepository personGroupRepository)
        {
            _faceSettings = faceSettings.Value;
            _logger = logger;
            _personGroupRepository = personGroupRepository;
        }

        public async Task<string> TrainAsync(List<string> imageSasUrls)
        {
            var personGroupId = Guid.NewGuid().ToString();
            var personId = Guid.NewGuid();
            var blobUtility = new BlobUtility();
            blobUtility.ConnectionString = Helper.GetEnvironmentVariable("BlobConnectionString");
            blobUtility.ContainerName = Helper.GetEnvironmentVariable("ContainerName");

            _logger.LogInformation($"Create a person group ({personGroupId}).");

            using var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSettings.SubscriptionKey)) { Endpoint = _faceSettings.EndPoint };

            // Create a Person Group
            await faceClient.PersonGroup.CreateAsync(personGroupId, $"Group: {personGroupId}", recognitionModel: RECOGNITION_MODEL4);

            var personGroupClient = faceClient.PersonGroupPerson;

            foreach (var sasUrl in imageSasUrls)
            {
                // Download the blob's contents to a memory stream
                byte[] blobContent = await blobUtility.DownloadBlobStreamAsync(sasUrl);

                IList<DetectedFace> detectedFaces;
                using (var detectionStream = new MemoryStream(blobContent))
                {
                    detectedFaces = await faceClient.Face.DetectWithStreamAsync(detectionStream,
                        recognitionModel: RECOGNITION_MODEL4,
                        detectionModel: DetectionModel.Detection03,
                        returnFaceAttributes: new List<FaceAttributeType> { FaceAttributeType.QualityForRecognition });
                }

                bool sufficientQuality;
                if (detectedFaces.Any())
                {
                    if (detectedFaces.Count > 1)
                    {
                        foreach (var detectedFace in detectedFaces)
                        {
                            sufficientQuality = IsQualityForRecognition(detectedFace);

                            if (sufficientQuality)
                            {
                                var person = await faceClient.PersonGroupPerson.CreateAsync(personGroupId, detectedFace.FaceId.ToString());

                                // Crop the image to only include the detected face
                                var faceRectangle = detectedFace.FaceRectangle;
                                var croppedImage = CropImage(blobContent, faceRectangle);

                                using var addFaceStream = new MemoryStream(croppedImage);
                                await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(personGroupId, person.PersonId, addFaceStream, detectedFace.FaceId.ToString());
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
                                var person = await faceClient.PersonGroupPerson.CreateAsync(personGroupId, detectedFace.FaceId.ToString());

                                // No need to crop the image as it contains only one face
                                using var addFaceStream = new MemoryStream(blobContent);
                                await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(personGroupId, person.PersonId, addFaceStream, detectedFace.FaceId.ToString());
                            }
                        }
                    }
                }
            }

            await TrainAndCheckStatusAsync(imageSasUrls, personGroupId, personId, faceClient);

            return personGroupId;
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

        public async Task<string> GetTrainingStatusAsync(string personGroupId)
        {
            using var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSettings.SubscriptionKey)) { Endpoint = _faceSettings.EndPoint };

            var trainingStatus = await faceClient.PersonGroup.GetTrainingStatusAsync(personGroupId);

            return trainingStatus.Status.ToString();
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

        private async Task SavePersonGroupToDb(string personGroupId, string personId, List<string> sasUrls)
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
                    PersonId = Guid.Parse(personId),
                    BlobName = blobName,
                    BlobUrl = blobUrl,
                    CreatedBy = "System"
                };

                await _personGroupRepository.InsertPersonGroupImageAsync(personGroupImage);
            }
        }

        private async Task TrainAndCheckStatusAsync(List<string> imageSasUrls, string personGroupId, Guid personId, FaceClient faceClient)
        {
            // TODO: This should be done in a background task
            await faceClient.PersonGroup.TrainAsync(personGroupId);

            // TODO: This should be done in a background task
            while (true)
            {
                await Task.Delay(1000);
                var trainingStatus = await faceClient.PersonGroup.GetTrainingStatusAsync(personGroupId);

                _logger.LogInformation($"Training status: {trainingStatus.Status}.");

                if (trainingStatus.Status == TrainingStatusType.Succeeded)
                {
                    await SavePersonGroupToDb(personGroupId, personId.ToString(), imageSasUrls);
                    break;
                }
            }
        }
    }
}