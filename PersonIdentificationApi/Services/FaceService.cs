using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using PersonIdentificationApi.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace PersonIdentificationApi.Services
{
    public class FaceService : IFaceService
    {
        const string RECOGNITION_MODEL4 = RecognitionModel.Recognition04;
        private readonly ILogger<FaceService> _logger;
        private readonly PersonGroupRepository _personGroupRepository;
        private readonly BlobUtility _blobUtility;
        private readonly IConfiguration _configuration;
        private readonly string _faceSubscriptionKey;
        private readonly string _faceEndPoint;

        public FaceService(
            ILogger<FaceService> logger,
            PersonGroupRepository personGroupRepository,
            IConfiguration configuration)
        {
            _configuration = configuration;
            _faceSubscriptionKey = _configuration.GetValue<string>("FaceSubscriptionKey");
            _faceEndPoint = _configuration.GetValue<string>("FaceEndPoint");

            if (string.IsNullOrWhiteSpace(_faceSubscriptionKey) || string.IsNullOrWhiteSpace(_faceEndPoint))
            {
                throw new Exception("FaceEndPoint and FaceSubscriptionKey must be set in the appsettings.json file.");
            }

            _logger = logger;
            _personGroupRepository = personGroupRepository;

            _blobUtility = new BlobUtility
            {
                ConnectionString = _configuration.GetValue<string>("BlobConnectionString"),
                ContainerName = _configuration.GetValue<string>("ContainerName")
            };

            if (string.IsNullOrWhiteSpace(_blobUtility.ConnectionString) || string.IsNullOrWhiteSpace(_blobUtility.ContainerName))
            {
                throw new Exception("BlobConnectionString and ContainerName are required.");
            }
        }

        // This is an example of identification using Azure AI Face service.
        // Assumes that a set of images have been trained and a person group has been created.
        // Person group and images trained are stored in the database. Images trained are stored in Azure Blob Storage.
        public async Task<List<DetectedFaceResponse>> DetectFaceRecognize(List<string> imagesToIdentify)
        {
            var detectedFaceResponse = new List<DetectedFaceResponse>();

            // Get all trained groups for lookup
            var dbDetectionResults = await _personGroupRepository.GetPersonGroupsAsync();
            using var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSubscriptionKey)) { Endpoint = _faceEndPoint };

            // Loop through all passed in images that need to be identified
            // Get all trained groups for lookup
            foreach (var imageToIdentify in imagesToIdentify)
            {
                foreach (var dbDetectionResult in dbDetectionResults)
                {
                    var sourceFaceIds = new List<Guid>();

                    // Detect faces from source image url.
                    var sasUri = _blobUtility.GetBlobSasUri(imageToIdentify);

                    var detectedFaces = await DetectFaceRecognize(faceClient, sasUri.ToString());

                    // Add detected faceId to sourceFaceIds.
                    foreach (var detectedFace in detectedFaces) { sourceFaceIds.Add(detectedFace.FaceId.Value); }

                    // Identify the faces in a person group. 
                    var identifyResults = await faceClient.Face.IdentifyAsync(sourceFaceIds, dbDetectionResult.PersonGroupId.ToString());

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
                            var person = await faceClient.PersonGroupPerson.GetAsync(dbDetectionResult.PersonGroupId.ToString(), candidate.PersonId);

                            if (person != null)
                            {
                                var response = new DetectedFaceResponse();
                                response.PersonGroupName = dbDetectionResult.PersonGroupName;
                                
                                _logger.LogInformation($"Person '{person.Name}' is identified for the face in: {imageToIdentify} - {identifyResult.FaceId}, confidence: {candidate.Confidence}.");

                                var verifyResult = await faceClient.Face.VerifyFaceToPersonAsync(identifyResult.FaceId, person.PersonId, dbDetectionResult.PersonGroupId.ToString());

                                response.PersonGroupId = dbDetectionResult.PersonGroupId.ToString();
                                response.PersonId = person.PersonId.ToString();
                                response.PersonName = person.Name;
                                response.SegmentedImageName = imageToIdentify;
                                response.SegmentedSasBlobUrl = sasUri.ToString();

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

        public async Task DeletePersonGroup(string personGroupId)
        {
            _logger.LogInformation($"Deleting person group: {personGroupId}");

            using var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSubscriptionKey)) { Endpoint = _faceEndPoint };

            await faceClient.PersonGroup.DeleteAsync(personGroupId);

            _logger.LogInformation($"Deleted person group: {personGroupId}");
        }

        public async Task<PersonGroup> GetPersonGroup(string personGroupId)
        {
            using var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSubscriptionKey)) { Endpoint = _faceEndPoint };
            var personGroup = await faceClient.PersonGroup.GetAsync(personGroupId);

            return personGroup;
        }

        // This is an example of how to train images using Azure AI Face service.
        // Stores the person group and images trained in the database.
        public async Task<string> TrainNewGroupAsync(List<string> imageSasUrls, string groupName, string personName)
        {
            var dbPersonGroup = new DbPersonGroup();
            var personGroupId = Guid.NewGuid().ToString();
            dbPersonGroup.PersonGroupId = Guid.Parse(personGroupId);
            dbPersonGroup.PersonGroupName = groupName;

            using var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSubscriptionKey)) { Endpoint = _faceEndPoint };

            _logger.LogInformation($"Create a person group ({personGroupId}).");

            // Create a Person Group
            await faceClient.PersonGroup.CreateAsync(personGroupId, groupName, recognitionModel: RECOGNITION_MODEL4);

            bool sufficientQuality;
            var imageName = string.Empty;
            Uri? sasUri = null;

            var dbPersonGroupPeople = new List<DbPersonGroupPerson>();
            var dbPersonFaces = new List<DbPersonGroupPersonFace>();

            foreach (var sasUrl in imageSasUrls)
            {
                sasUri = new Uri(sasUrl);
                imageName = Path.GetFileName(sasUri.LocalPath);

                // Download the blob's contents to a memory stream
                byte[] blobContent = await _blobUtility.DownloadBlobStreamAsync(sasUrl);

                IList<DetectedFace> detectedFaces = await GetDetectedFaces(faceClient, blobContent);

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
                                var person = await faceClient.PersonGroupPerson.CreateAsync(personGroupId, personName);

                                var dbPersonGroupPerson = new DbPersonGroupPerson();
                                dbPersonGroupPerson.PersonId = person.PersonId;
                                dbPersonGroupPerson.PersonName = personName;
                                
                                // Crop the image to only include the detected face
                                var faceRectangle = detectedFace.FaceRectangle;
                                var croppedImage = CropImage(blobContent, faceRectangle);

                                using var addFaceStream = new MemoryStream(croppedImage);
                                await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(personGroupId, person.PersonId, addFaceStream, detectedFace.FaceId.ToString());

                                dbPersonGroupPerson.PersonGroupId = Guid.Parse(personGroupId);
                                var blobUrl = sasUri.GetLeftPart(UriPartial.Path);
                                var blobName = Path.GetFileName(sasUri.LocalPath);
                                dbPersonGroupPeople.Add(dbPersonGroupPerson);

                                dbPersonFaces.Add(new DbPersonGroupPersonFace
                                {
                                    FaceId = detectedFace.FaceId.Value,
                                    PersonId = person.PersonId,
                                    BlobName = blobName,
                                    BlobUrl = blobUrl,
                                    IsTrained = true
                                });
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
                                var person = await faceClient.PersonGroupPerson.CreateAsync(personGroupId, personName);
                                
                                var dbPersonGroupPerson = new DbPersonGroupPerson();
                                dbPersonGroupPerson.PersonId = person.PersonId;
                                dbPersonGroupPerson.PersonName = personName;

                                // No need to crop the image as it contains only one face
                                using var addFaceStream = new MemoryStream(blobContent);

                                // handling event where a face is not detected to prevent the app from crashing
                                try
                                {
                                    await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(personGroupId, person.PersonId, addFaceStream, detectedFace.FaceId.ToString());

                                    dbPersonGroupPerson.PersonGroupId = Guid.Parse(personGroupId);
                                    var blobUrl = sasUri.GetLeftPart(UriPartial.Path);
                                    var blobName = Path.GetFileName(sasUri.LocalPath);
                                    dbPersonGroupPeople.Add(dbPersonGroupPerson);

                                    dbPersonFaces.Add(new DbPersonGroupPersonFace
                                    {
                                        FaceId = detectedFace.FaceId.Value,
                                        PersonId = person.PersonId,
                                        BlobName = blobName,
                                        BlobUrl = blobUrl,
                                        IsTrained = true
                                    });
                                }
                                catch (Exception e)
                                {
                                    _logger.LogWarning($"Error adding face to person group: {e.Message}");
                                }
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
            await TrainAndCheckStatusAsync(personGroupId, faceClient, dbPersonGroup, dbPersonGroupPeople, dbPersonFaces);

            return personGroupId;
        }

        public async Task TrainExistingGroup(List<string> imageSasUrls, string personGroupId, string personName)
        {
            var existingPersonGroup = await _personGroupRepository.GetPersonGroupAsync(personGroupId, personName);
            using var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSubscriptionKey)) { Endpoint = _faceEndPoint };

            bool sufficientQuality;
            var imageName = string.Empty;
            Uri? sasUri = null;
            var dbPersonFaces = new List<DbPersonGroupPersonFace>();
            var dbPersonGroupPeople = new List<DbPersonGroupPerson>();

            if (existingPersonGroup != null)
            {
                foreach (var sasUrl in imageSasUrls)
                {
                    sasUri = new Uri(sasUrl);
                    imageName = Path.GetFileName(sasUri.LocalPath);

                    // Download the blob's contents to a memory stream
                    byte[] blobContent = await _blobUtility.DownloadBlobStreamAsync(sasUrl);

                    IList<DetectedFace> detectedFaces = await GetDetectedFaces(faceClient, blobContent);

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
                                    var person = await faceClient.PersonGroupPerson.GetAsync(personGroupId.ToString(), existingPersonGroup.PersonId);

                                    // Crop the image to only include the detected face
                                    var faceRectangle = detectedFace.FaceRectangle;
                                    var croppedImage = CropImage(blobContent, faceRectangle);

                                    using var addFaceStream = new MemoryStream(croppedImage);
                                    await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(personGroupId.ToString(), person.PersonId, addFaceStream, detectedFace.FaceId.ToString());
                                    
                                    var blobUrl = sasUri.GetLeftPart(UriPartial.Path);
                                    var blobName = Path.GetFileName(sasUri.LocalPath);

                                    dbPersonFaces.Add(new DbPersonGroupPersonFace
                                    {
                                        FaceId = detectedFace.FaceId.Value,
                                        PersonId = person.PersonId,
                                        BlobName = blobName,
                                        BlobUrl = blobUrl,
                                        IsTrained = true
                                    });
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
                                    var person = await faceClient.PersonGroupPerson.GetAsync(personGroupId.ToString(), existingPersonGroup.PersonId);

                                    // No need to crop the image as it contains only one face
                                    using var addFaceStream = new MemoryStream(blobContent);

                                    // handling event where a face is not detected to prevent the app from crashing
                                    try
                                    {
                                        await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(personGroupId.ToString(), person.PersonId, addFaceStream, detectedFace.FaceId.ToString());

                                        var blobUrl = sasUri.GetLeftPart(UriPartial.Path);
                                        var blobName = Path.GetFileName(sasUri.LocalPath);

                                        dbPersonFaces.Add(new DbPersonGroupPersonFace
                                        {
                                            FaceId = detectedFace.FaceId.Value,
                                            PersonId = person.PersonId,
                                            BlobName = blobName,
                                            BlobUrl = blobUrl,
                                            IsTrained = true
                                        });
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.LogWarning($"Error adding face to person group: {e.Message}");
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation($"Insuffucient quality for recognition for detected face. FaceId: {detectedFace.FaceId} Image: {imageName}");
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Add new person to existing group
               _logger.LogInformation($"Adding new person to group {personGroupId}.");

                foreach (var sasUrl in imageSasUrls)
                {
                    sasUri = new Uri(sasUrl);
                    imageName = Path.GetFileName(sasUri.LocalPath);

                    // Download the blob's contents to a memory stream
                    byte[] blobContent = await _blobUtility.DownloadBlobStreamAsync(sasUrl);

                    IList<DetectedFace> detectedFaces = await GetDetectedFaces(faceClient, blobContent);

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
                                    var person = await faceClient.PersonGroupPerson.CreateAsync(personGroupId, personName);

                                    // Crop the image to only include the detected face
                                    var faceRectangle = detectedFace.FaceRectangle;
                                    var croppedImage = CropImage(blobContent, faceRectangle);

                                    using var addFaceStream = new MemoryStream(croppedImage);
                                    await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(personGroupId.ToString(), person.PersonId, addFaceStream, detectedFace.FaceId.ToString());

                                    var blobUrl = sasUri.GetLeftPart(UriPartial.Path);
                                    var blobName = Path.GetFileName(sasUri.LocalPath);

                                    dbPersonFaces.Add(new DbPersonGroupPersonFace
                                    {
                                        FaceId = detectedFace.FaceId.Value,
                                        PersonId = person.PersonId,
                                        BlobName = blobName,
                                        BlobUrl = blobUrl,
                                        IsTrained = true
                                    });
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
                                    var person = await faceClient.PersonGroupPerson.CreateAsync(personGroupId, personName);

                                    var dbPersonGroupPerson = new DbPersonGroupPerson();
                                    dbPersonGroupPerson.PersonId = person.PersonId;
                                    dbPersonGroupPerson.PersonName = personName;

                                    // No need to crop the image as it contains only one face
                                    using var addFaceStream = new MemoryStream(blobContent);

                                    // handling event where a face is not detected to prevent the app from crashing
                                    try
                                    {
                                        await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(personGroupId.ToString(), person.PersonId, addFaceStream, detectedFace.FaceId.ToString());

                                        dbPersonGroupPerson.PersonGroupId = Guid.Parse(personGroupId);
                                        dbPersonGroupPeople.Add(dbPersonGroupPerson);

                                        var blobUrl = sasUri.GetLeftPart(UriPartial.Path);
                                        var blobName = Path.GetFileName(sasUri.LocalPath);

                                        dbPersonFaces.Add(new DbPersonGroupPersonFace
                                        {
                                            FaceId = detectedFace.FaceId.Value,
                                            PersonId = person.PersonId,
                                            BlobName = blobName,
                                            BlobUrl = blobUrl,
                                            IsTrained = true
                                        });
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.LogWarning($"Error adding face to person group: {e.Message}");
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation($"Insuffucient quality for recognition for detected face. FaceId: {detectedFace.FaceId} Image: {imageName}");
                                }
                            }
                        }
                    }
                }
            }

            if (dbPersonFaces.Any())
            {
                // TODO: This should be done in a background process
                await TrainAndCheckStatusAsync(personGroupId.ToString(), faceClient, dbPersonGroupPeople, dbPersonFaces);
            }
        }

        public async Task<string> GetTrainingStatusAsync(string personGroupId)
        {
            using var faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_faceSubscriptionKey)) { Endpoint = _faceEndPoint };

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
            // TODO: bypassing quality check for now
            bool sufficientQuality = true;
            //var faceQualityForRecognition = detectedFace.FaceAttributes.QualityForRecognition;

            ////  Only "high" quality images are recommended for person enrollment
            //if (faceQualityForRecognition.HasValue && (faceQualityForRecognition.Value != QualityForRecognition.High))
            //{
            //    sufficientQuality = false;
            //}

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

        private async Task<IList<DetectedFace>> GetDetectedFaces(FaceClient faceClient, byte[] blobContent)
        {
            IList<DetectedFace> detectedFaces;

            using (var detectionStream = new MemoryStream(blobContent))
            {
                detectedFaces = await faceClient.Face.DetectWithStreamAsync(detectionStream,
                    recognitionModel: RECOGNITION_MODEL4,
                    detectionModel: DetectionModel.Detection03,
                    returnFaceAttributes: new List<FaceAttributeType> { FaceAttributeType.QualityForRecognition });
            }

            return detectedFaces;
        }

        /// <summary>
        /// Trains and checks status. Assumes that a set of images have been trained and a person group has been created.
        /// </summary>
        /// <param name="personGroupId"></param>
        /// <param name="faceClient"></param>
        /// <param name="dbPersonGroup"></param>
        /// <param name="dbPersonGroupPeople"></param>
        /// <param name="dbPersonFaces"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task TrainAndCheckStatusAsync(
            string personGroupId, 
            FaceClient faceClient, 
            DbPersonGroup dbPersonGroup, 
            List<DbPersonGroupPerson> dbPersonGroupPeople, 
            List<DbPersonGroupPersonFace> dbPersonFaces)
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
                    await _personGroupRepository.CreatePersonGroupAllAsync(dbPersonGroup, dbPersonGroupPeople, dbPersonFaces);
                    break;
                }

                if (trainingStatus.Status == TrainingStatusType.Failed)
                {
                    throw new Exception("Training failed");
                }
            }
        }

        /// <summary>
        /// Trains and checks status. Assumes a group is already created and new faces are being added for training.
        /// </summary>
        /// <param name="personGroupId"></param>
        /// <param name="faceClient"></param>
        /// <param name="dbPersonFaces"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task TrainAndCheckStatusAsync(
            string personGroupId,
            FaceClient faceClient,
            List<DbPersonGroupPerson> dbPersonGroupPeople,
            List<DbPersonGroupPersonFace> dbPersonFaces)
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
                    await _personGroupRepository.AddFacesToExistingPerson(personGroupId, dbPersonGroupPeople, dbPersonFaces);
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