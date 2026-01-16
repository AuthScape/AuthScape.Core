using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class RequestTimeoutException : Exception
    {

        public RequestTimeoutException() : base() { }

        public RequestTimeoutException(string message)
            : base(message) { }

        public RequestTimeoutException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public RequestTimeoutException(string message, Exception innerException)
            : base(message, innerException) { }

        public RequestTimeoutException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected RequestTimeoutException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
