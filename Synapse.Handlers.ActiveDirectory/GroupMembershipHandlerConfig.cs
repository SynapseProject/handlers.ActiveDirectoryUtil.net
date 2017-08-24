using System.Xml.Serialization;

public class GroupMembershipHandlerConfig
{
    [XmlElement]
    public string DefaultDomain { get; set; }
}