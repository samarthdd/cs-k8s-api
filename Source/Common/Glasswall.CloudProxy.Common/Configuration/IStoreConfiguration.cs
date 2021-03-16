namespace Glasswall.CloudProxy.Common.Configuration
{
    public interface IStoreConfiguration
    {
        string OriginalStorePath { get; set; }
        string RebuiltStorePath { get; set; }
    }
}
