namespace PersonIdentificationApi.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using PersonIdentificationApi.Models;
    using PersonIdentificationApi.Services;
    using System.Text.Json;

    [ApiController]
    [Route("[controller]")]
    public class PersonIdentificationRunner : ControllerBase
    {
        private readonly IFaceService _faceService;
        private readonly ILogger<PersonIdentificationRunner> _logger;
        private readonly IConfiguration _configuration;
        private readonly BlobUtility _blobUtility;
        private readonly ISegmentation _segmentation;

        public PersonIdentificationRunner(
            IFaceService faceService,
            ISegmentation segmentation,
            ILogger<PersonIdentificationRunner> logger, 
            IConfiguration configuration)
        {
            _faceService = faceService;
            _segmentation = segmentation;
            _logger = logger;
            _configuration = configuration;

            _blobUtility = new BlobUtility
            {
                ConnectionString = _configuration.GetValue<string>("BlobConnectionString"),
                ContainerName = _configuration.GetValue<string>("ContainerName")
            };
        }

        /// <summary>
        /// This API will accept a JSON payload in the format of:
        /// {
        ///   "personGroupName": "",
        ///   "processTrainingModel": {
        ///     "personName": "",
        ///     "images": [
        ///       {
        ///         "filename": "<name of file>"
        ///       }
        ///     ]
        ///   }
        /// }
        /// </summary>
        /// <returns>Group Id Created</returns>
        [HttpPost("training/train")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> CreateTrainingGroup([FromBody] TrainingRequest trainingRequest)
        {
            try
            {
                if (trainingRequest.ProcessTrainingModel.Images.Count == 0)
                {
                    return BadRequest("No images provided for training");
                }

                if (string.IsNullOrEmpty(trainingRequest.PersonGroupName))
                {
                    return BadRequest("No group name provided for training");
                }
                
                if (string.IsNullOrEmpty(trainingRequest.ProcessTrainingModel.PersonName))
                {
                    return BadRequest("No person name provided for training");
                }

                var imagesToTrain = new List<string>();

                // get the URI of each image in the container
                foreach (Image image in trainingRequest.ProcessTrainingModel.Images)
                {
                    Uri imageUri = _blobUtility.GetBlobSasUri(image.Filename);

                    if (imageUri == null)
                    {
                        _logger.LogWarning($"File does not exist: {image.Filename}");
                    }
                    else
                    {
                        imagesToTrain.Add(imageUri.ToString());
                    }
                }

                // run the training process
                var groupId = await _faceService.TrainNewGroupAsync(imagesToTrain, trainingRequest.PersonGroupName, trainingRequest.ProcessTrainingModel.PersonName);

                return Ok(groupId);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// This API will accept a JSON payload in the format of:
        /// {
        ///   "personGroupName": "",
        ///   "processTrainingModel": {
        ///     "personName": "",
        ///     "images": [
        ///       {
        ///         "filename": "<name of file>"
        ///       }
        ///     ]
        ///   }
        /// }
        /// </summary>
        /// <returns>Group Id Created</returns>
        [HttpPut("training/train")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> TrainExistingGroup([FromBody] TrainingRequest trainingRequest)
        {
            try
            {
                if (trainingRequest.ProcessTrainingModel.Images.Count == 0)
                {
                    return BadRequest("No images provided for training");
                }

                if (string.IsNullOrEmpty(trainingRequest.ProcessTrainingModel.PersonName))
                {
                    return BadRequest("No person name provided for training");
                }

                if (string.IsNullOrEmpty(trainingRequest.PersonGroupId))
                {
                    return BadRequest("No group id provided for training");
                }

                var imagesToTrain = new List<string>();

                // get the URI of each image in the container
                foreach (Image image in trainingRequest.ProcessTrainingModel.Images)
                {
                    Uri imageUri = _blobUtility.GetBlobSasUri(image.Filename);

                    if (imageUri == null)
                    {
                        _logger.LogWarning($"File does not exist: {image.Filename}");
                    }
                    else
                    {
                        imagesToTrain.Add(imageUri.ToString());
                    }
                }

                // run the training process
                await _faceService.TrainExistingGroup(imagesToTrain, trainingRequest.PersonGroupId, trainingRequest.ProcessTrainingModel.PersonName);

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// This API will accept a JSON payload in the format of
        /// {
        ///   "images": [    // there should be 1 or more images
        ///     {
        ///       "filename": "<name of file>"
        ///     }
        ///   ]
        /// }
        /// </summary>
        /// <returns></returns>
        [HttpPost("training/identification")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> Identification([FromBody] ProcessIdentificationModel processModel)
        {
            List<DetectedFaceResponse> identificationResponse = new List<DetectedFaceResponse>();
            try
            {
                // get the URI of each image in the container
                foreach (Image image in processModel.Images)
                {
                    Uri imageUri = _blobUtility.GetBlobSasUri(image.Filename);

                    if (imageUri == null)
                    {
                        _logger.LogWarning($"File does not exist: {image.Filename}");
                    }
                    else
                    {
                        // run the pipeline to include
                        // - call segmentation API (return list of person objects)
                        List<string> segmentedImages = await _segmentation.RunSegmentation(processModel.Images[0].Filename, imageUri.AbsoluteUri);
                        // - loop through each person object and
                        //   - call Face API
                        //   - call OCR API
                        identificationResponse = await _faceService.DetectFaceRecognize(segmentedImages);
                        _logger.LogInformation($"Identification response: {JsonSerializer.Serialize(identificationResponse)}");
                    }
                }

                // TODO: Need to return an aggregate response of the identification process.
                return Ok(identificationResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("trainingStatus/{personGroupId}")]
        public async Task<IActionResult> GetTrainingStatusAsync(string personGroupId)
        {
            try
            {
                var trainingStatus = await _faceService.GetTrainingStatusAsync(personGroupId);
                return Ok(trainingStatus);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpDelete("deletePersonGroup/{personGroupId}")]
        public async Task<IActionResult> DeletePersonGroup(string personGroupId)
        {
            try
            {
                await _faceService.DeletePersonGroup(personGroupId);
                return Ok();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("NotFound"))
                {
                    return NotFound();
                }

                return StatusCode(500, ex.Message);
            }
        }
    }
}