namespace PersonIdentificationApi.Utilities
{
    using System.Text.Json.Serialization;

    public class ProcessModel
    {
        [JsonPropertyName("images")]
        public List<Image> Images { get; set; }

        [JsonPropertyName("process")]
        public string Process {  get; set; }
    }

    public class Image
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; }
    }
}