namespace AuthScape.IDP.Services.ErrorTracking;

public interface IErrorGroupingService
{
    string GenerateErrorSignature(string errorType, string stackTrace, string endpoint);
    string GetConciseErrorMessage(string errorMessage, int maxLength = 1000);
    bool ShouldGroupErrors(string signature1, string signature2);
    string GetSimpleErrorType(string fullTypeName);
}
