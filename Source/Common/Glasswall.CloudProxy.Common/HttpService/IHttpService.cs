using System.Threading.Tasks;

namespace Glasswall.CloudProxy.Common.HttpService
{
    public interface IHttpService
    {
        Task<(byte[] Data, string Error)> GetFileBytes(string uri);
    }
}
