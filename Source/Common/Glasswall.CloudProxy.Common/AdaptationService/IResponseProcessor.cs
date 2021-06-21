using System;
using System.Collections.Generic;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public interface IResponseProcessor : IDisposable
    {
        IAdaptationServiceResponse Process(IDictionary<string, object> headers, byte[] body);
    }
}
