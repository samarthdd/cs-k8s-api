using System;
using System.Threading;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public interface IAdaptationServiceClient<IResponseProcessor> : IDisposable
    {
        void Connect();
        IAdaptationServiceResponse AdaptationRequest(Guid fileId, string originalStoreFilePath, string rebuiltStoreFilePath, CancellationToken processingCancellationToken);
    }
}
