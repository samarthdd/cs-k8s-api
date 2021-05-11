using System.Collections.Generic;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public interface IResponseProcessor
    {
        IAdaptationServiceResponse Process(IDictionary<string, object> headers, byte[] body);
    }
}
