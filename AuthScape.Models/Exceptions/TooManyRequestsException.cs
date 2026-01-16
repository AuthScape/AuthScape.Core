using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class TooManyRequestsException : Exception
    {

        public TooManyRequestsException() : base() { }

        public TooManyRequestsException(string message)
            : base(message) { }

        public TooManyRequestsException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public TooManyRequestsException(string message, Exception innerException)
            : base(message, innerException) { }

        public TooManyRequestsException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected TooManyRequestsException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
