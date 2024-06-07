using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

namespace PersonIdentificationApi.Models
{
    public class DetectedFaceResponse
    {
        public string PersonGroupId { get; set; }
        public string PersonGroupName { get; set; }
        public string ImageToIdentify { get; set; }
        public string BlobName { get; set; }
        public string BlobUrl { get; set; }
        public string PersonId { get; internal set; }
        public List<FacesIdentified> FacesIdentified { get; set; }
    }

    public class  FacesIdentified
    {
        public string FaceId { get; set; }
        public VerifyResult VerifyResults { get; internal set; }
    }
}
