using FluentValidation;

namespace Connect.Application.Features.Calls.Commands.InitiateCall;

public class InitiateCallCommandValidator : AbstractValidator<InitiateCallCommand>
{
    public InitiateCallCommandValidator()
    {
        RuleFor(x => x.CalleeId)
            .NotEmpty().WithMessage("Callee ID is required.");
    }
}
