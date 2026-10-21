using FluentValidation;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;

namespace SupportDeskAI.Validators;

// Filters are optional: a null filter is always valid.
public class TicketQueryParametersValidator : AbstractValidator<TicketQueryParameters>
{
    public TicketQueryParametersValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is null || Enum.IsDefined(typeof(TicketStatus), s.Value))
            .WithMessage("Status must be 0 (Open), 1 (InProgress), 2 (Resolved) or 3 (Closed).");

        RuleFor(x => x.Priority)
            .Must(p => p is null || Enum.IsDefined(typeof(TicketPriority), p.Value))
            .WithMessage("Priority must be 0 (Low), 1 (Medium), 2 (High) or 3 (Critical).");

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters.");
    }
}
