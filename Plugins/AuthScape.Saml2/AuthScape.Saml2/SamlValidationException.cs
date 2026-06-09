namespace AuthScape.Saml2;

public class SamlValidationException : Exception
{
    public SamlValidationException(string message) : base(message) { }
    public SamlValidationException(string message, Exception inner) : base(message, inner) { }
}
