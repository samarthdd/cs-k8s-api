using System;
using System.Threading.Tasks;

namespace Glasswall.CloudProxy.Common.HttpService
{
    public interface IHttpService : IDisposable
    {
        Task<(byte[] Data, string Error)> GetFileBytes(string uri);
    }
}
