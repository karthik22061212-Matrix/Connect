using FluentValidation;

namespace Connect.Application.Features.Blocking.Commands.BlockUser;

public class BlockUserCommandValidator : AbstractValidator<BlockUserCommand>
{
    public BlockUserCommandValidator()
    {
        RuleFor(x => x.UserIdToBlock)
            .NotEmpty().WithMessage("UserIdToBlock is required.");
    }
}
