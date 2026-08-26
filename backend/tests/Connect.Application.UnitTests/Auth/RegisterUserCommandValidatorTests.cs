using Connect.Application.Features.Auth.Commands.RegisterUser;
using FluentValidation.TestHelper;
using Xunit;

namespace Connect.Application.UnitTests.Auth;

public class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_Password_Is_Valid()
    {
        var command = new RegisterUserCommand("valid_user", "test@example.com", "Password123!");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_Fail_When_Password_Is_Too_Short()
    {
        var command = new RegisterUserCommand("valid_user", "test@example.com", "Pass1!");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public void Should_Fail_When_Password_Lacks_Uppercase()
    {
        var command = new RegisterUserCommand("valid_user", "test@example.com", "password123!");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Should_Fail_When_Password_Lacks_Lowercase()
    {
        var command = new RegisterUserCommand("valid_user", "test@example.com", "PASSWORD123!");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Should_Fail_When_Password_Lacks_Digit()
    {
        var command = new RegisterUserCommand("valid_user", "test@example.com", "Password!!!");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public void Should_Fail_When_Password_Lacks_SpecialCharacter()
    {
        var command = new RegisterUserCommand("valid_user", "test@example.com", "Password1234");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must contain at least one special character.");
    }
}
