using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class ForbiddenException : Exception
    {

        public ForbiddenException() : base() { }

        public ForbiddenException(string message)
            : base(message) { }

        public ForbiddenException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public ForbiddenException(string message, Exception innerException)
            : base(message, innerException) { }

        public ForbiddenException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected ForbiddenException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
