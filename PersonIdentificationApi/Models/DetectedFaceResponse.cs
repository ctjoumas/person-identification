using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System.Text.Json.Serialization;

namespace PersonIdentificationApi.Models
{
    public class DetectedFaceResponse
    {
        public string PersonGroupId { get; set; }
        public string PersonId { get; internal set; }
        public string PersonGroupName { get; set; }
        [JsonIgnore]
        public string ImageTrained { get; set; }
        [JsonIgnore]
        public string TrainedBlobUrl { get; set; }
        public string SegmentedImageName { get; set; }
        public string SegmentedSasBlobUrl { get; set; }
        
        public List<FacesIdentified> FacesIdentified { get; set; }
    }

    public class  FacesIdentified
    {
        public string FaceId { get; set; }
        public VerifyResult VerifyResults { get; internal set; }
    }
}
