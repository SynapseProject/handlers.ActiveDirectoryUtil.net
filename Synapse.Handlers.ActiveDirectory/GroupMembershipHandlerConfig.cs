using System.Collections.Generic;
using System.Xml.Serialization;

public class GroupMembershipHandlerConfig
{
    [XmlElement]
    public List<string> ValidDomains { get; set; }
}