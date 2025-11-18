using System.Text.Json.Serialization;

namespace ToggleStatisticsBot;

public class ToggleRequestDto
{
    [JsonPropertyName("start_date")]
    public string StartDate { get; set; }
    [JsonPropertyName("end_date")]
    public string EndDate { get; set; }
    [JsonPropertyName("description")]
    public string Description { get; set; }
}