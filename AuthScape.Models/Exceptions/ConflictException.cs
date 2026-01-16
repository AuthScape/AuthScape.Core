using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class ConflictException : Exception
    {

        public ConflictException() : base() { }

        public ConflictException(string message)
            : base(message) { }

        public ConflictException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public ConflictException(string message, Exception innerException)
            : base(message, innerException) { }

        public ConflictException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected ConflictException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
