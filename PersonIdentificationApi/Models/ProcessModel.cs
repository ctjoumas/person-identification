namespace PersonIdentificationApi.Models
{
    using System.Text.Json.Serialization;

    public class ProcessModel
    {
        [JsonPropertyName("images")]
        public List<Image> Images { get; set; }

        [JsonPropertyName("process")]
        public string Process { get; set; }

        [JsonPropertyName("personGroupName")]
        public string GroupName { get; set; }
    }

    public class Image
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; }
    }
}