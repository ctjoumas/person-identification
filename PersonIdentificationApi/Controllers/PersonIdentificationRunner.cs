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
        /// This API will accept a JSON payload in the format of
        /// {
        ///   "images": [    // there should be 1 or more images
        ///     {
        ///       "filename": "<name of file>"
        ///     },
        ///     {
        ///       "filename: "<name of file>"
        ///     }
        ///   ]
        ///   "process": "<process>"  // process should be "Training"
        /// }
        /// </summary>
        /// <returns></returns>
        [HttpPost("training/train")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> TrainingRunner([FromBody] ProcessModel processModel)
        {
            try
            {
                if (processModel.Images.Count == 0)
                {
                    return BadRequest("No images provided for training");
                }

                if (string.IsNullOrEmpty(processModel.GroupName))
                {
                    return BadRequest("No group name provided for training");
                }

                if (string.IsNullOrEmpty(processModel.Process))
                {
                    return BadRequest("No process provided for training");
                }

                var imagesToTrain = new List<string>();
                
                if (processModel.Process.Equals("Training"))
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
                            imagesToTrain.Add(imageUri.ToString());
                        }
                    }
                }
                else
                {
                    return BadRequest($"Invalid process specified. Recieved '{processModel.Process}' but expected 'Training'");
                }

                // run the training process
                var groupId = await _faceService.TrainAsync(imagesToTrain, processModel.GroupName);

                return Ok(groupId);
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
        ///   "process": "<process>"  // process should be "Identification"
        /// }
        /// </summary>
        /// <returns></returns>
        [HttpPost("training/identification")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> IdentificationRunner([FromBody] ProcessModel processModel)
        {
            List<Models.DetectedFaceResponse> identificationResponse = new List<Models.DetectedFaceResponse>();
            try
            {               
                if (processModel.Process.Equals("Identification"))
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
                }
                else
                {
                    return BadRequest($"Invalid process specified. Recieved '{processModel.Process}' but expected 'Identification'");
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