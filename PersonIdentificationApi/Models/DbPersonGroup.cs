namespace PersonIdentificationApi.Models
{
    public class DbPersonGroup
    {
        public Guid PersonGroupId { get; set; }
        public bool? IsTrained { get; set; }
        public bool? IsDeleted { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}