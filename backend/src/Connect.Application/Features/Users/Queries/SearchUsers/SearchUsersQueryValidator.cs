using FluentValidation;

namespace Connect.Application.Features.Users.Queries.SearchUsers;

public class SearchUsersQueryValidator : AbstractValidator<SearchUsersQuery>
{
    public SearchUsersQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Search query must not be empty.")
            .MaximumLength(100).WithMessage("Search query must not exceed 100 characters.");
    }
}
