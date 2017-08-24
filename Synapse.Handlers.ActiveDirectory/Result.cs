using Newtonsoft.Json;

public class Result
{
    [JsonProperty(PropertyName = "user")]
    public string User { get; set; }

    [JsonProperty(PropertyName = "group")]
    public string Group { get; set; }

    [JsonProperty(PropertyName = "action")]
    public string Action { get; set; }

    [JsonProperty(PropertyName = "exitCode")]
    public int ExitCode { get; set; }

    [JsonProperty(PropertyName = "note")]
    public string Note { get; set; }
}