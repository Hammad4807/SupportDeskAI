using SupportDeskAI.Enums;

namespace SupportDeskAI.Results;

// Returned by service methods that can fail in more than one expected way
// (not found, invalid input, AI problems). No exceptions are thrown for these cases:
// the caller checks IsSuccess and decides what to do (the controller maps it to a status code).
public class ServiceResult<T>
{
    public bool IsSuccess => ErrorType == ServiceErrorType.None;
    public T? Value { get; private init; }
    public ServiceErrorType ErrorType { get; private init; }
    public string? ErrorMessage { get; private init; }

    // Field name -> messages, e.g. { "Title": ["Title is required."] }
    public IDictionary<string, string[]>? ValidationErrors { get; private init; }

    public static ServiceResult<T> Success(T value) => new() { Value = value };

    public static ServiceResult<T> Failure(ServiceErrorType errorType, string message) =>
        new() { ErrorType = errorType, ErrorMessage = message };

    public static ServiceResult<T> ValidationFailed(IDictionary<string, string[]> errors) =>
        new() { ErrorType = ServiceErrorType.Validation, ErrorMessage = "One or more validation errors occurred.", ValidationErrors = errors };

    public static ServiceResult<T> NotFound(Guid id) =>
        Failure(ServiceErrorType.NotFound, $"No ticket exists with id {id}.");
}
