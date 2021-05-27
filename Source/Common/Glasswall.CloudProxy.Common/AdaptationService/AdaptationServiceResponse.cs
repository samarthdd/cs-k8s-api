using System;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public class AdaptationServiceResponse : IAdaptationServiceResponse
    {
        public Guid FileId { get; set; }
        public ReturnOutcome FileOutcome { get; set; }
        public string SourceFileLocation { get; set; }
        public string RebuiltFileLocation { get; set; }
        public string SourcePresignedUrl { get; set; }
        public string CleanPresignedUrl { get; set; }
        public string ReportPresignedUrl { get; set; }
        public string GwLogPresignedUrl { get; set; }
        public string LogPresignedUrl { get; set; }
        public string MetaDataPresignedUrl { get; set; }
        public string SDKEngineVersion { get; set; }
        public RebuildProcessingStatus? RebuildProcessingStatus { get; set; }
    }
}
