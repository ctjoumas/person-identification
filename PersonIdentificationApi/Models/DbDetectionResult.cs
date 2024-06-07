namespace PersonIdentificationApi.Models
{
    public class DbDetectionResult
    {
        public Guid PersonGroupId { get; set; }
        public string PersonGroupName { get; set; }
        public string BlobName { get; set; }
        public string BlobUrl { get; set; }
    }
}