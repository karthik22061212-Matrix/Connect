using FluentValidation;

namespace Connect.Application.Features.ConnectRequests.Commands.AcceptConnectRequest;

public class AcceptConnectRequestCommandValidator : AbstractValidator<AcceptConnectRequestCommand>
{
    public AcceptConnectRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Request ID is required.");
    }
}
