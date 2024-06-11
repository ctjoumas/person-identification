namespace PersonIdentificationApi.Models
{
    using Newtonsoft.Json;

    public class TrainingRequest
    {
        [JsonProperty("personGroupName")]
        public string PersonGroupName { get; set; }

        [JsonProperty("personGroupId")]
        public string? PersonGroupId { get; set; }

        [JsonProperty("processModel")]
        public ProcessTrainingModel ProcessTrainingModel { get; set; }
    }
       
    public class ProcessTrainingModel
    {
        [JsonProperty("personName")]
        public string PersonName { get; set; }

        [JsonProperty("images")]
        public List<Image> Images { get; set; }
    }

    public class ProcessIdentificationModel
    {
        [JsonProperty("images")]
        public List<Image> Images { get; set; }
    }

    public class Image
    {
        [JsonProperty("filename")]
        public string Filename { get; set; }
    }
}