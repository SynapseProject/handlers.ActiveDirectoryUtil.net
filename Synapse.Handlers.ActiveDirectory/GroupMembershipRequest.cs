using System.Collections.Generic;
using Newtonsoft.Json;

public class GroupMembershipRequest
{
    public List<AddSection> AddSection { get; set; }

    public List<DeleteSection> DeleteSection { get; set; }
}