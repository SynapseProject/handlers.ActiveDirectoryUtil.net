using System.Collections.Generic;
using Newtonsoft.Json;

namespace Synapse.Handlers.ActiveDirectory
{
    public class GroupMembershipResponse
    {
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }

        [JsonProperty(PropertyName = "results")]
        public List<Result> Results { get; set; }
    }
}