using System.Runtime.Serialization;

namespace AuthScape.Models.Exceptions
{
    [Serializable]
    public class GoneException : Exception
    {

        public GoneException() : base() { }

        public GoneException(string message)
            : base(message) { }

        public GoneException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public GoneException(string message, Exception innerException)
            : base(message, innerException) { }

        public GoneException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected GoneException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
