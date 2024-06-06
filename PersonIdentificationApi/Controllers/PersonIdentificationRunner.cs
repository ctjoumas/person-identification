namespace PersonIdentificationApi.Controllers
{
    using Microsoft.AspNetCore.DataProtection.KeyManagement;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CognitiveServices.Vision.Face;
    using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
    using Microsoft.Extensions.Options;
    using PersonIdentification.FaceService;
    using PersonIdentificationApi.Helpers;
    using PersonIdentificationApi.Utilities;
    using System.Text.Json;
    using static System.Net.WebRequestMethods;

    [ApiController]
    [Route("[controller]")]
    public class PersonIdentificationRunner : ControllerBase
    {
        private readonly IFaceService _faceService;
        private readonly ILogger<PersonIdentificationRunner> _logger;
        private readonly FaceSettings _faceSettings;

        public PersonIdentificationRunner(IFaceService faceService, ILogger<PersonIdentificationRunner> logger, IOptions<FaceSettings> faceSettings)
        {
            _faceService = faceService;
            _logger = logger;
            _faceSettings = faceSettings.Value;
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
                    BlobUtility blobUtility = new BlobUtility();
                    blobUtility.ConnectionString = Helper.GetEnvironmentVariable("BlobConnectionString");
                    blobUtility.ContainerName = Helper.GetEnvironmentVariable("ContainerName");

                    // get the URI of each image in the container
                    foreach (Image image in processModel.Images)
                    {
                        Uri imageUri = blobUtility.GetBlobSasUri(image.Filename);

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
            try
            {               
                if (processModel.Process.Equals("Identification"))
                {
                    BlobUtility blobUtility = new BlobUtility();
                    blobUtility.ConnectionString = Helper.GetEnvironmentVariable("BlobConnectionString");
                    blobUtility.ContainerName = Helper.GetEnvironmentVariable("ContainerName");

                    // get the URI of each image in the container
                    foreach (Image image in processModel.Images)
                    {
                        Uri imageUri = blobUtility.GetBlobSasUri(image.Filename);

                        if (imageUri == null)
                        {
                            _logger.LogWarning($"File does not exist: {image.Filename}");
                        }
                        else
                        {
                            // run the pipeline to include
                            // - call segmentation API (return list of person objects)
                            Segmentation segmentation = new Segmentation(processModel.Images[0].Filename, imageUri.AbsoluteUri);
                            List<string> segmentedImages = await segmentation.RunSegmentation();
                            // - loop through each person object and
                            //   - call Face API
                            //   - call OCR API
                            var imagesToIdentify = processModel.Images.Select(x => x.Filename).ToList();
                            var identificationResponse = await _faceService.DetectFaceRecognize(imagesToIdentify);
                            _logger.LogInformation($"Identification response: {JsonSerializer.Serialize(identificationResponse)}");
                        }
                    }
                }
                else
                {
                    return BadRequest($"Invalid process specified. Recieved '{processModel.Process}' but expected 'Identification'");
                }

                // TODO: Need to return an aggregate response of the identification process.
                return Ok("Long-running process started in the background");
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