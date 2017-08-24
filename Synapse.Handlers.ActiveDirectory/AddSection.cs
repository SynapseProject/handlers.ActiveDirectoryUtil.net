using System.Collections.Generic;
using Newtonsoft.Json;

public class AddSection
{
    [JsonProperty( PropertyName = "domain" )]
    public string Domain { get; set; }

    [JsonProperty(PropertyName = "groups")]
    public List<string> Groups { get; set; }

    [JsonProperty(PropertyName = "users")]
    public List<string> Users { get; set; }
}