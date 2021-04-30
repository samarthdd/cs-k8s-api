namespace Glasswall.CloudProxy.Common.Configuration
{
    public interface IVersionConfiguration
    {
        public string SDKEngineVersion { get; set; }
        public string SDKApiVersion { get; set; }
    }
}
