using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class NotImplementedHttpException : Exception
    {

        public NotImplementedHttpException() : base() { }

        public NotImplementedHttpException(string message)
            : base(message) { }

        public NotImplementedHttpException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public NotImplementedHttpException(string message, Exception innerException)
            : base(message, innerException) { }

        public NotImplementedHttpException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected NotImplementedHttpException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
