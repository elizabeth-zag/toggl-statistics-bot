namespace ToggleStatisticsBot;

public class TimeParams : Params<TimeSpan>
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public string Task { get; set; }
}