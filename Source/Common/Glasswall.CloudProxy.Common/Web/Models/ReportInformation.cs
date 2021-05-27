namespace Glasswall.CloudProxy.Common.Web.Models
{
    public class ReportInformation
    {
        public byte[] ReportBytes { get; set; }
        public byte[] MetadaBytes { get; set; }
        public string ReportXmlText { get; set; }
        public string MetadaJsonText { get; set; }
    }
}
