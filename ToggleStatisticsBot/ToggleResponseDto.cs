using System.Text.Json.Serialization;

namespace ToggleStatisticsBot;

public class ToggleResponseDto
{
    [JsonPropertyName("description")] 
    public string Description { get; set; }

    [JsonPropertyName("time_entries")]
    public List<TogglTimeEntry> TimeEntries { get; set; }
}