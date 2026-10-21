using FluentValidation;
using SupportDeskAI.Constants;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;

namespace SupportDeskAI.Validators;

// Treats the AI answer as untrusted input: every value must be one of the allowed values
// (or a short text), otherwise the whole answer is rejected and nothing is saved.
public class AiAnalysisResultValidator : AbstractValidator<AiAnalysisResult>
{
    public AiAnalysisResultValidator()
    {
        RuleFor(x => x.Category)
            .Must(c => AiAllowedValues.Match(AiAllowedValues.Categories, c) is not null)
            .WithMessage(x => $"Category '{x.Category}' is not one of: {string.Join(", ", AiAllowedValues.Categories)}.");

        // Only the names Low/Medium/High/Critical are accepted (not numbers like "7").
        RuleFor(x => x.Priority)
            .Must(p => Enum.GetNames<TicketPriority>().Any(n => string.Equals(n, p?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage(x => $"Priority '{x.Priority}' is not one of: Low, Medium, High, Critical.");

        RuleFor(x => x.Sentiment)
            .Must(s => AiAllowedValues.Match(AiAllowedValues.Sentiments, s) is not null)
            .WithMessage(x => $"Sentiment '{x.Sentiment}' is not one of: {string.Join(", ", AiAllowedValues.Sentiments)}.");

        RuleFor(x => x.Summary)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Summary is empty.")
            .MaximumLength(AiAllowedValues.SummaryMaxLength).WithMessage($"Summary is longer than {AiAllowedValues.SummaryMaxLength} characters.");

        RuleFor(x => x.SuggestedAction)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Suggested action is empty.")
            .MaximumLength(AiAllowedValues.SuggestedActionMaxLength).WithMessage($"Suggested action is longer than {AiAllowedValues.SuggestedActionMaxLength} characters.");
    }
}
