namespace PersonIdentificationApi.Models
{
    public class DbPersonGroupImage
    {
        public Guid PersonId { get; set; }
        public Guid PersonGroupId { get; set; }
        public string BlobName { get; set; }
        public string BlobUrl { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}