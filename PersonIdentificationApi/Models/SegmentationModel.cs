namespace PersonIdentificationApi.Models
{
    using System.Text.Json.Serialization;

    public class SegmentationModel
    {
        [JsonPropertyName("boxes")]
        public List<BoxObject> Boxes { get; set; }
    }

    public class BoxObject
    {
        [JsonPropertyName("box")]
        public Box Box { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("polygon")]
        public List<List<float>> Polygon { get; set; }
    }

    public class Box
    {
        [JsonPropertyName("topX")]
        public double TopX { get; set; }

        [JsonPropertyName("topY")]
        public double TopY { get; set; }

        [JsonPropertyName("bottomX")]
        public double BottomX { get; set; }

        [JsonPropertyName("bottomY")]
        public double BottomY { get; set; }
    }
}