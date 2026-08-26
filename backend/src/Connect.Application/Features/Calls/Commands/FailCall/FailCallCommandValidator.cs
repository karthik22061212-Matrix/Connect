using FluentValidation;

namespace Connect.Application.Features.Calls.Commands.FailCall;

public class FailCallCommandValidator : AbstractValidator<FailCallCommand>
{
    public FailCallCommandValidator()
    {
        RuleFor(v => v.CallId)
            .NotEmpty().WithMessage("Call ID is required.");
    }
}
