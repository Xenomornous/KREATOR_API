using System.Text.Json.Serialization;
namespace Kreator_API.Models
{
    

    public class LectureJsonDto
    {
        [JsonPropertyName("lectureId")]
        public string LectureId { get; set; }

        [JsonPropertyName("lectureName")]
        public string LectureName { get; set; }

        [JsonPropertyName("modUser")]
        public string ModUser { get; set; }

        [JsonPropertyName("lectureJsonContent")]
        public string LectureJsonContent { get; set; }

        [JsonPropertyName("modDate")]
        public DateTime ModDate { get; set; }
    }
}