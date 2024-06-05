using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

namespace PersonIdentificationApi.Models
{
    public class DetectedFaceResponse
    {
        public string PersonGroupId { get; set; }
        public string FaceId { get; set; }
        public string BlobName { get; set; }
        public string BlobUrl { get; set; }
        public string PersonId { get; internal set; }
        public List<VerifyResult> VerifyResults { get; internal set; }
    }
}
