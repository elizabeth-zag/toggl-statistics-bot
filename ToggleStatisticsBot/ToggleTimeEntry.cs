using System.Text.Json.Serialization;

namespace ToggleStatisticsBot;

public class TogglTimeEntry
{
    [JsonPropertyName("start")] 
    public DateTimeOffset Start { get; set; }

    [JsonPropertyName("stop")] 
    public DateTimeOffset Stop { get; set; }
}