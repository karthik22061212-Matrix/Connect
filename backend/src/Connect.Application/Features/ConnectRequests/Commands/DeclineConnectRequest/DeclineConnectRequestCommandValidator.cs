using FluentValidation;

namespace Connect.Application.Features.ConnectRequests.Commands.DeclineConnectRequest;

public class DeclineConnectRequestCommandValidator : AbstractValidator<DeclineConnectRequestCommand>
{
    public DeclineConnectRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");
    }
}
