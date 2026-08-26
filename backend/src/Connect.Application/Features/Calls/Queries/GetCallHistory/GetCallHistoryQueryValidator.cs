using FluentValidation;

namespace Connect.Application.Features.Calls.Queries.GetCallHistory;

public class GetCallHistoryQueryValidator : AbstractValidator<GetCallHistoryQuery>
{
    public GetCallHistoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage("PageSize must be at least 1.")
            .LessThanOrEqualTo(100).WithMessage("PageSize must not exceed 100.");
    }
}
