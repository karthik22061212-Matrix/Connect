using FluentValidation;

namespace Connect.Application.Features.Reports.Commands.ReportUser;

public class ReportUserCommandValidator : AbstractValidator<ReportUserCommand>
{
    public ReportUserCommandValidator()
    {
        RuleFor(x => x.ReportedUserId)
            .NotEmpty().WithMessage("ReportedUserId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(100).WithMessage("Reason must not exceed 100 characters.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Note must not exceed 1000 characters.");
    }
}
