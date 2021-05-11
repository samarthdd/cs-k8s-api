using System;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public interface IAdaptationServiceResponse
    {
        public Guid FileId { get; set; }
        public ReturnOutcome FileOutcome { get; set; }
        public string SourceFileLocation { get; set; }
        public string RebuiltFileLocation { get; set; }
        public string SourcePresignedUrl { get; set; }
        public string CleanPresignedUrl { get; set; }
        public string ReportPresignedUrl { get; set; }
    }
}
