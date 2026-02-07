namespace AuthScape.AI.Exceptions;

public class AIServiceException : Exception
{
    public AIServiceException(string message) : base(message) { }
    public AIServiceException(string message, Exception innerException) : base(message, innerException) { }
}
