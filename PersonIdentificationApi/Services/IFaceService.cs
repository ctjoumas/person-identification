namespace PersonIdentification.FaceService
{
    public interface IFaceService
    {
        Task<string> TrainAsync(List<string> imagesToTrain);
        Task<string> GetTrainingStatusAsync(string personGroupId);
    }
}
