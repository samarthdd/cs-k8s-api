using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public interface IStoreConfiguration : IDisposable
    {
        string OriginalStorePath { get; set; }
        string RebuiltStorePath { get; set; }
    }
}
