using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using PersonIdentificationApi.Models;

namespace PersonIdentificationApi.Services
{
    public interface IFaceService
    {
        Task<string> TrainAsync(List<string> imageSasUrls, string groupName, string personName);
        Task<string> GetTrainingStatusAsync(string personGroupId);
        Task<List<DetectedFaceResponse>> DetectFaceRecognize(List<string> imagesToIdentify);
        Task DeletePersonGroup(string personGroupId);
        Task<PersonGroup> GetPersonGroup(string personGroupId);
    }
}