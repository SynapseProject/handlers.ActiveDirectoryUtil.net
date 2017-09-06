using System.Collections.Generic;
using Newtonsoft.Json;

public class GroupMembershipResponse
{
    [JsonProperty( PropertyName = "status" )]
    public string Status { get; set; }

    [JsonProperty( PropertyName = "results" )]
    public List<Result> Results { get; set; }
}