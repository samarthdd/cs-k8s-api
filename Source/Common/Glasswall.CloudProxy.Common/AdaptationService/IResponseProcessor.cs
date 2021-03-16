using System.Collections.Generic;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public interface IResponseProcessor
    {
        ReturnOutcome Process(IDictionary<string, object> headers, byte[] body);
    }
}
