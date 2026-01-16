using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class MethodNotAllowedException : Exception
    {

        public MethodNotAllowedException() : base() { }

        public MethodNotAllowedException(string message)
            : base(message) { }

        public MethodNotAllowedException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public MethodNotAllowedException(string message, Exception innerException)
            : base(message, innerException) { }

        public MethodNotAllowedException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected MethodNotAllowedException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
