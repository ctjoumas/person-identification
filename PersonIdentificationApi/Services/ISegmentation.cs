namespace PersonIdentificationApi.Services
{
    public interface ISegmentation
    {
        Task<List<string>> RunSegmentation(string fileName, string imageUrl);
    }
}
