namespace SupportDeskAI.Enums;

// Why a service call failed. Controllers turn each value into an HTTP status code.
public enum ServiceErrorType
{
    None,            // success
    NotFound,        // 404
    Validation,      // 400
    AiNotConfigured, // 503 - no API key / provider set up
    AiTimeout,       // 504 - the AI provider took too long
    AiProviderError, // 502 - the AI provider returned an error or was unreachable
    AiInvalidOutput  // 502 - the AI answered, but the answer failed validation
}
