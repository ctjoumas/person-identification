namespace PersonIdentificationApi.Models
{
    public class DbPersonFace
    {
        public Guid FaceId { get; set; }
        public Guid PersonId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}