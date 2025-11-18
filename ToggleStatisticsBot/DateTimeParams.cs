using System.Text.Json.Serialization;

namespace ToggleStatisticsBot;

public class DateTimeParams : Params<DateTime>
{
    [JsonConverter(typeof(CustomDateTimeConverter))]
    public DateTime Start { get; set; }
    [JsonConverter(typeof(CustomDateTimeConverter))]
    public DateTime End { get; set; }
    public string Task { get; set; }
}