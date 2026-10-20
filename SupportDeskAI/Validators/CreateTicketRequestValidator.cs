using FluentValidation;
using SupportDeskAI.DTOs;

namespace SupportDeskAI.Validators;

public class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        // Cascade.Stop: report only the first failing rule per field
        // (and never run the length check on a null/empty value).
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
    }
}
