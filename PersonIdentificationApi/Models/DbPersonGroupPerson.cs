namespace PersonIdentificationApi.Models
{
    public class DbPersonGroupPerson
    {
        public Guid PersonId { get; set; }
        public Guid PersonGroupId { get; set; }
        public string? PersonName { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}