using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public class InvalidApplicationConfigurationException : ApplicationException
    {
        public InvalidApplicationConfigurationException() : base() { }
        public InvalidApplicationConfigurationException(string message) : base(message) { }
        public InvalidApplicationConfigurationException(string message, System.Exception inner) : base(message, inner) { }

        // A constructor is needed for serialization when an
        // exception propagates from a remoting server to the client.
        protected InvalidApplicationConfigurationException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
