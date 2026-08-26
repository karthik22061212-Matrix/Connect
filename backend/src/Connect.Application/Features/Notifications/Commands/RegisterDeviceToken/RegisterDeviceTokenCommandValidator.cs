using FluentValidation;

namespace Connect.Application.Features.Notifications.Commands.RegisterDeviceToken;

public class RegisterDeviceTokenCommandValidator : AbstractValidator<RegisterDeviceTokenCommand>
{
    public RegisterDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Device token is required.")
            .MaximumLength(500).WithMessage("Device token must not exceed 500 characters.");

        RuleFor(x => x.Platform)
            .IsInEnum().WithMessage("Invalid device platform.");
    }
}
