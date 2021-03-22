using System;
using System.Runtime.Serialization;

namespace Glasswall.CloudProxy.Common.QueueAccess
{
    [Serializable]
    internal class InvalidMessageException : Exception
    {
        public InvalidMessageException()
        {
        }

        public InvalidMessageException(string message) : base(message)
        {
        }

        public InvalidMessageException(string actualMessage, string expectedMessage)
            : base($"Received '{actualMessage}' when '{expectedMessage}' was expected")
        {
        }

        public InvalidMessageException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidMessageException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}