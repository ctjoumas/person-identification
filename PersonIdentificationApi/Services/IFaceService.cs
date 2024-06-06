using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using PersonIdentificationApi.Models;

namespace PersonIdentification.FaceService
{
    public interface IFaceService
    {
        Task<string> TrainAsync(List<string> imagesToTrain);
        Task<string> GetTrainingStatusAsync(string personGroupId);
        Task<List<DetectedFaceResponse>> DetectFaceRecognize(List<string> imagesToIdentify);
        Task DeletePersonGroup(string personGroupId);
        Task<PersonGroup> GetPersonGroup(string personGroupId);
    }
}