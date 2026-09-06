using Connect.Domain.Enums;
using FluentValidation;

namespace Connect.Application.Features.PresenceSettings.Commands.UpdatePresenceSettings;

public class UpdatePresenceSettingsCommandValidator : AbstractValidator<UpdatePresenceSettingsCommand>
{
    public UpdatePresenceSettingsCommandValidator()
    {
        RuleFor(v => v.Visibility)
            .IsInEnum().WithMessage("Invalid presence visibility setting.");
    }
}
