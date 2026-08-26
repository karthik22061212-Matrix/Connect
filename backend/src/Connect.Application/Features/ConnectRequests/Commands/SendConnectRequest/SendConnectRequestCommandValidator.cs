using FluentValidation;

namespace Connect.Application.Features.ConnectRequests.Commands.SendConnectRequest;

public class SendConnectRequestCommandValidator : AbstractValidator<SendConnectRequestCommand>
{
    public SendConnectRequestCommandValidator()
    {
        RuleFor(x => x.ToUserId)
            .NotEmpty().WithMessage("Target user ID is required.");
    }
}
