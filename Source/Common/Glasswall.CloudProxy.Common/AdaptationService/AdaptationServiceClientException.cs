using System;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public class AdaptationServiceClientException : ApplicationException
    {
        public AdaptationServiceClientException()
        {

        }

        public AdaptationServiceClientException(string message) : base(message)
        {
        }
    }
}
