namespace Glasswall.CloudProxy.Common.Configuration
{
    public class AdaptationStoreConfiguration : IStoreConfiguration
    {
        public string OriginalStorePath { get; set; }
        public string RebuiltStorePath { get; set; }
    }
}
