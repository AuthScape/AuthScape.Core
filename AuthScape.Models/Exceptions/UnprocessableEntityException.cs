using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class UnprocessableEntityException : Exception
    {

        public UnprocessableEntityException() : base() { }

        public UnprocessableEntityException(string message)
            : base(message) { }

        public UnprocessableEntityException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public UnprocessableEntityException(string message, Exception innerException)
            : base(message, innerException) { }

        public UnprocessableEntityException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected UnprocessableEntityException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
