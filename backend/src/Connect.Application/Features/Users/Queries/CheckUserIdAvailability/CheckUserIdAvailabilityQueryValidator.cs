using FluentValidation;

namespace Connect.Application.Features.Users.Queries.CheckUserIdAvailability;

public class CheckUserIdAvailabilityQueryValidator : AbstractValidator<CheckUserIdAvailabilityQuery>
{
    public CheckUserIdAvailabilityQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.")
            .MaximumLength(30).WithMessage("User ID must not exceed 30 characters.");
    }
}
