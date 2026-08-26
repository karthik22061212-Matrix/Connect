using FluentValidation;

namespace Connect.Application.Features.Presence.Commands.UpdatePresence;

public class UpdatePresenceCommandValidator : AbstractValidator<UpdatePresenceCommand>
{
    public UpdatePresenceCommandValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Invalid presence status.");
    }
}
