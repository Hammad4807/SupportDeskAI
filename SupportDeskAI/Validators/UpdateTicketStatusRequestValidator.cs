using FluentValidation;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;

namespace SupportDeskAI.Validators;

public class UpdateTicketStatusRequestValidator : AbstractValidator<UpdateTicketStatusRequest>
{
    public UpdateTicketStatusRequestValidator()
    {
        // Enum.IsDefined rejects numbers that are not one of the enum values (e.g. 7).
        RuleFor(x => x.Status)
            .Must(s => Enum.IsDefined(typeof(TicketStatus), s))
            .WithMessage("Status must be 0 (Open), 1 (InProgress), 2 (Resolved) or 3 (Closed).");
    }
}
