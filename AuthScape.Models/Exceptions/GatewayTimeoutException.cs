using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class GatewayTimeoutException : Exception
    {

        public GatewayTimeoutException() : base() { }

        public GatewayTimeoutException(string message)
            : base(message) { }

        public GatewayTimeoutException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public GatewayTimeoutException(string message, Exception innerException)
            : base(message, innerException) { }

        public GatewayTimeoutException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected GatewayTimeoutException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
