using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class ServiceUnavailableException : Exception
    {

        public ServiceUnavailableException() : base() { }

        public ServiceUnavailableException(string message)
            : base(message) { }

        public ServiceUnavailableException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public ServiceUnavailableException(string message, Exception innerException)
            : base(message, innerException) { }

        public ServiceUnavailableException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected ServiceUnavailableException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
