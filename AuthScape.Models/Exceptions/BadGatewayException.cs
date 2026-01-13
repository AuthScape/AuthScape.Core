using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class BadGatewayException : Exception
    {

        public BadGatewayException() : base() { }

        public BadGatewayException(string message)
            : base(message) { }

        public BadGatewayException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public BadGatewayException(string message, Exception innerException)
            : base(message, innerException) { }

        public BadGatewayException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected BadGatewayException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
