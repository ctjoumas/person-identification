namespace PersonIdentificationApi.Models
{
    public class DbDetectionResult
    {
        public Guid PersonGroupId { get; set; }
        public Guid PersonId { get; set; }
        public string PersonName { get; set; }
        public string PersonGroupName { get; set; }
        public string BlobName { get; set; }
        public string BlobUrl { get; set; }
    }
}