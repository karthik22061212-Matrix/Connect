using FluentValidation;

namespace Connect.Application.Features.Calls.Commands.EndCall;

public class EndCallCommandValidator : AbstractValidator<EndCallCommand>
{
    public EndCallCommandValidator()
    {
        RuleFor(x => x.CallId)
            .NotEmpty().WithMessage("Call ID is required.");
    }
}
