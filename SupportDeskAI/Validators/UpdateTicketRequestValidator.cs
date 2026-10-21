using FluentValidation;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;

namespace SupportDeskAI.Validators;

public class UpdateTicketRequestValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketRequestValidator()
    {
        RuleFor(x => x.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Title is required.")
            .Must(t => t.Trim().Length >= 5).WithMessage("Title must be at least 5 characters.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Description is required.")
            .Must(d => d.Trim().Length >= 10).WithMessage("Description must be at least 10 characters.")
            .MaximumLength(4000).WithMessage("Description must not exceed 4000 characters.");

        // Enum.IsDefined rejects numbers that are not one of the enum values (e.g. 7).
        RuleFor(x => x.Priority)
            .Must(p => Enum.IsDefined(typeof(TicketPriority), p))
            .WithMessage("Priority must be 0 (Low), 1 (Medium), 2 (High) or 3 (Critical).");
    }
}
