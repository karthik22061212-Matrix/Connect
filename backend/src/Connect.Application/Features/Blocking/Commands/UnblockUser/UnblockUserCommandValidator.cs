using FluentValidation;

namespace Connect.Application.Features.Blocking.Commands.UnblockUser;

public class UnblockUserCommandValidator : AbstractValidator<UnblockUserCommand>
{
    public UnblockUserCommandValidator()
    {
        RuleFor(x => x.UserIdToUnblock)
            .NotEmpty().WithMessage("UserIdToUnblock is required.");
    }
}
