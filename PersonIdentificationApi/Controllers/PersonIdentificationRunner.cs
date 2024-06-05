namespace PersonIdentificationApi.Controllers
{
    using Microsoft.AspNetCore.DataProtection.KeyManagement;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.CognitiveServices.Vision.Face;
    using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
    using PersonIdentificationApi.Helpers;
    using PersonIdentificationApi.Utilities;
    using System.Text.Json;
    using static System.Net.WebRequestMethods;

    [ApiController]
    [Route("[controller]")]
    public class PersonIdentificationRunner : ControllerBase
    {
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
        [HttpPost("StartTraining")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> TrainingRunner()
        {
            try
            {
                // pull out the JSON from the body and deserialize it in order to get the images and the process 
                var request = HttpContext.Request;
                using var stream = new StreamReader(request.Body);
                var body = stream.ReadToEndAsync();

                var processModel = JsonSerializer.Deserialize<ProcessModel>(body.Result);

                if (processModel.Process.Equals("Training"))
                {

                    BlobUtility blobUtility = new BlobUtility();
                    blobUtility.ConnectionString = Helper.GetEnvironmentVariable("BlobConnectionString");
                    blobUtility.ContainerName = Helper.GetEnvironmentVariable("ContainerName");

                    // get the URI of each image in the container
                    foreach (Image image in processModel.Images)
                    {
                        Uri imageUri = blobUtility.GetBlobUri(image.Filename);

                        if (imageUri == null)
                        {
                            Console.WriteLine($"File does not exist: {image.Filename}");
                        }
                    }
                }
                else
                {
                    return BadRequest($"Invalid process specified. Recieved '{processModel.Process}' but expected 'Training'");
                }

                // run the training process
                // TBD


                return Ok("Long-running process started in the background");
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occured: {ex.Message}");
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
        [HttpGet("StartIdentification")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> IdentificationRunner()
        {
            try
            {
                // pull out the JSON from the body and deserialize it in order to get the images and the process 
                var request = HttpContext.Request;
                using var stream = new StreamReader(request.Body);
                var body = stream.ReadToEndAsync();

                var processModel = JsonSerializer.Deserialize<ProcessModel>(body.Result);

                if (processModel.Process.Equals("Identification"))
                {
                    BlobUtility blobUtility = new BlobUtility();
                    blobUtility.ConnectionString = Helper.GetEnvironmentVariable("BlobConnectionString");
                    blobUtility.ContainerName = Helper.GetEnvironmentVariable("ContainerName");

                    // assuming there is only one image to be processed
                    Uri imageUri = blobUtility.GetBlobUri(processModel.Images[0].Filename);

                    if (imageUri == null)
                    {
                        Console.WriteLine($"File does not exist: {processModel.Images[0].Filename}");
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
                    }
                }
                else
                {
                    return BadRequest($"Invalid process specified. Recieved '{processModel.Process}' but expected 'Identification'");
                }                

                return Ok("Long-running process started in the background");
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occured: {ex.Message}");
            }
        }
    }
}