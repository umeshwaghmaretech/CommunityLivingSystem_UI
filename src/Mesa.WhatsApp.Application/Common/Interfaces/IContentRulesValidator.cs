namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface IContentRulesValidator
{
    ContentValidationResult ValidateOutboundBody(string businessName, string body);
    string BuildCompliantBody(string businessName, string middleContent);
}

public sealed record ContentValidationResult(bool IsValid, IReadOnlyList<string> Errors);
