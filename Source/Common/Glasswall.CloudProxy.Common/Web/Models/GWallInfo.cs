using System.Collections.Generic;
using System.Xml.Serialization;

namespace Glasswall.CloudProxy.Common.Web.Models
{
    [XmlRoot(ElementName = "DocumentSummary", Namespace = "http://glasswall.com/namespace")]
    public class DocumentSummary
    {
        [XmlElement(ElementName = "TotalSizeInBytes", Namespace = "http://glasswall.com/namespace")]
        public string TotalSizeInBytes { get; set; }
        [XmlElement(ElementName = "FileType", Namespace = "http://glasswall.com/namespace")]
        public string FileType { get; set; }
        [XmlElement(ElementName = "Version", Namespace = "http://glasswall.com/namespace")]
        public string Version { get; set; }
    }

    [XmlRoot(ElementName = "ContentSwitch", Namespace = "http://glasswall.com/namespace")]
    public class ContentSwitch
    {
        [XmlElement(ElementName = "ContentName", Namespace = "http://glasswall.com/namespace")]
        public string ContentName { get; set; }
        [XmlElement(ElementName = "ContentValue", Namespace = "http://glasswall.com/namespace")]
        public string ContentValue { get; set; }
    }

    [XmlRoot(ElementName = "Camera", Namespace = "http://glasswall.com/namespace")]
    public class Camera
    {
        [XmlElement(ElementName = "ContentSwitch", Namespace = "http://glasswall.com/namespace")]
        public List<ContentSwitch> ContentSwitch { get; set; }
        [XmlAttribute(AttributeName = "cameraName")]
        public string CameraName { get; set; }
    }

    [XmlRoot(ElementName = "ContentManagementPolicy", Namespace = "http://glasswall.com/namespace")]
    public class ContentManagementPolicy
    {
        [XmlElement(ElementName = "Camera", Namespace = "http://glasswall.com/namespace")]
        public List<Camera> Camera { get; set; }
    }

    [XmlRoot(ElementName = "ContentItem", Namespace = "http://glasswall.com/namespace")]
    public class ContentItem
    {
        [XmlElement(ElementName = "TechnicalDescription", Namespace = "http://glasswall.com/namespace")]
        public string TechnicalDescription { get; set; }
        [XmlElement(ElementName = "InstanceCount", Namespace = "http://glasswall.com/namespace")]
        public string InstanceCount { get; set; }
        [XmlElement(ElementName = "TotalSizeInBytes", Namespace = "http://glasswall.com/namespace")]
        public string TotalSizeInBytes { get; set; }
        [XmlElement(ElementName = "AverageSizeInBytes", Namespace = "http://glasswall.com/namespace")]
        public string AverageSizeInBytes { get; set; }
        [XmlElement(ElementName = "MinSizeInBytes", Namespace = "http://glasswall.com/namespace")]
        public string MinSizeInBytes { get; set; }
        [XmlElement(ElementName = "MaxSizeInBytes", Namespace = "http://glasswall.com/namespace")]
        public string MaxSizeInBytes { get; set; }
    }

    [XmlRoot(ElementName = "ContentItems", Namespace = "http://glasswall.com/namespace")]
    public class ContentItems
    {
        [XmlElement(ElementName = "ContentItem", Namespace = "http://glasswall.com/namespace")]
        public List<ContentItem> ContentItem { get; set; }
        [XmlAttribute(AttributeName = "itemCount")]
        public string ItemCount { get; set; }
    }

    [XmlRoot(ElementName = "SanitisationItems", Namespace = "http://glasswall.com/namespace")]
    public class SanitisationItems
    {
        [XmlAttribute(AttributeName = "itemCount")]
        public string ItemCount { get; set; }
        [XmlElement(ElementName = "SanitisationItem", Namespace = "http://glasswall.com/namespace")]
        public SanitisationItem SanitisationItem { get; set; }
    }

    [XmlRoot(ElementName = "RemedyItem", Namespace = "http://glasswall.com/namespace")]
    public class RemedyItem
    {
        [XmlElement(ElementName = "TechnicalDescription", Namespace = "http://glasswall.com/namespace")]
        public string TechnicalDescription { get; set; }
        [XmlElement(ElementName = "InstanceCount", Namespace = "http://glasswall.com/namespace")]
        public string InstanceCount { get; set; }
    }

    [XmlRoot(ElementName = "RemedyItems", Namespace = "http://glasswall.com/namespace")]
    public class RemedyItems
    {
        [XmlElement(ElementName = "RemedyItem", Namespace = "http://glasswall.com/namespace")]
        public List<RemedyItem> RemedyItem { get; set; }
        [XmlAttribute(AttributeName = "itemCount")]
        public string ItemCount { get; set; }
    }

    [XmlRoot(ElementName = "IssueItems", Namespace = "http://glasswall.com/namespace")]
    public class IssueItems
    {
        [XmlAttribute(AttributeName = "itemCount")]
        public string ItemCount { get; set; }
    }

    [XmlRoot(ElementName = "ContentGroup", Namespace = "http://glasswall.com/namespace")]
    public class ContentGroup
    {
        [XmlElement(ElementName = "BriefDescription", Namespace = "http://glasswall.com/namespace")]
        public string BriefDescription { get; set; }
        [XmlElement(ElementName = "ContentItems", Namespace = "http://glasswall.com/namespace")]
        public ContentItems ContentItems { get; set; }
        [XmlElement(ElementName = "SanitisationItems", Namespace = "http://glasswall.com/namespace")]
        public SanitisationItems SanitisationItems { get; set; }
        [XmlElement(ElementName = "RemedyItems", Namespace = "http://glasswall.com/namespace")]
        public RemedyItems RemedyItems { get; set; }
        [XmlElement(ElementName = "IssueItems", Namespace = "http://glasswall.com/namespace")]
        public IssueItems IssueItems { get; set; }
    }

    [XmlRoot(ElementName = "SanitisationItem", Namespace = "http://glasswall.com/namespace")]
    public class SanitisationItem
    {
        [XmlElement(ElementName = "TechnicalDescription", Namespace = "http://glasswall.com/namespace")]
        public string TechnicalDescription { get; set; }
        [XmlElement(ElementName = "SanitisationId", Namespace = "http://glasswall.com/namespace")]
        public string SanitisationId { get; set; }
        [XmlElement(ElementName = "InstanceCount", Namespace = "http://glasswall.com/namespace")]
        public string InstanceCount { get; set; }
        [XmlElement(ElementName = "TotalSizeInBytes", Namespace = "http://glasswall.com/namespace")]
        public string TotalSizeInBytes { get; set; }
        [XmlElement(ElementName = "AverageSizeInBytes", Namespace = "http://glasswall.com/namespace")]
        public string AverageSizeInBytes { get; set; }
        [XmlElement(ElementName = "MinSizeInBytes", Namespace = "http://glasswall.com/namespace")]
        public string MinSizeInBytes { get; set; }
        [XmlElement(ElementName = "MaxSizeInBytes", Namespace = "http://glasswall.com/namespace")]
        public string MaxSizeInBytes { get; set; }
    }

    [XmlRoot(ElementName = "ContentGroups", Namespace = "http://glasswall.com/namespace")]
    public class ContentGroups
    {
        [XmlElement(ElementName = "ContentGroup", Namespace = "http://glasswall.com/namespace")]
        public List<ContentGroup> ContentGroup { get; set; }
        [XmlAttribute(AttributeName = "groupCount")]
        public string GroupCount { get; set; }
    }

    [XmlRoot(ElementName = "ExtractedItems", Namespace = "http://glasswall.com/namespace")]
    public class ExtractedItems
    {
        [XmlAttribute(AttributeName = "itemCount")]
        public string ItemCount { get; set; }
    }

    [XmlRoot(ElementName = "DocumentStatistics", Namespace = "http://glasswall.com/namespace")]
    public class DocumentStatistics
    {
        [XmlElement(ElementName = "DocumentSummary", Namespace = "http://glasswall.com/namespace")]
        public DocumentSummary DocumentSummary { get; set; }
        [XmlElement(ElementName = "ContentManagementPolicy", Namespace = "http://glasswall.com/namespace")]
        public ContentManagementPolicy ContentManagementPolicy { get; set; }
        [XmlElement(ElementName = "ContentGroups", Namespace = "http://glasswall.com/namespace")]
        public ContentGroups ContentGroups { get; set; }
        [XmlElement(ElementName = "ExtractedItems", Namespace = "http://glasswall.com/namespace")]
        public ExtractedItems ExtractedItems { get; set; }
    }

    [XmlRoot(ElementName = "GWallInfo", Namespace = "http://glasswall.com/namespace")]
    public class GWallInfo
    {
        [XmlElement(ElementName = "DocumentStatistics", Namespace = "http://glasswall.com/namespace")]
        public DocumentStatistics DocumentStatistics { get; set; }
        [XmlAttribute(AttributeName = "schemaLocation", Namespace = "http://www.w3.org/2001/XMLSchema-instance")]
        public string SchemaLocation { get; set; }
        [XmlAttribute(AttributeName = "xsi", Namespace = "http://www.w3.org/2000/xmlns/")]
        public string Xsi { get; set; }
        [XmlAttribute(AttributeName = "gw", Namespace = "http://www.w3.org/2000/xmlns/")]
        public string Gw { get; set; }
    }

}
