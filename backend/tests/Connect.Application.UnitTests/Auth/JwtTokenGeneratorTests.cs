using System.IdentityModel.Tokens.Jwt;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Connect.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Connect.Application.UnitTests.Auth;

public class JwtTokenGeneratorTests
{
    private const string ValidSecret = "ThisIsASuperSecretKeyForTestingJwtTokenGeneration12345!";

    [Fact]
    public void GenerateToken_WithValidSettings_ReturnsValidJwtToken()
    {
        // Arrange
        var jwtSettings = new JwtSettings
        {
            Secret = ValidSecret,
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        };

        var options = Options.Create(jwtSettings);
        var generator = new JwtTokenGenerator(options);

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserId = "user123",
            Email = "test@example.com",
            SubscriptionTier = SubscriptionTier.Free
        };

        // Act
        var tokenString = generator.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        Assert.False(string.IsNullOrWhiteSpace(tokenString));
        Assert.Equal("TestIssuer", jwtToken.Issuer);
    }

    [Fact]
    public void JwtSettingsValidation_WithMissingSecret_ThrowsOptionsValidationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", "" },
            { "JwtSettings:Issuer", "ConnectApi" },
            { "JwtSettings:Audience", "ConnectClient" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();

        // Act & Assert
        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JwtSettings>>().Value);
        Assert.Contains("JwtSettings:Secret", ex.Message);
    }

    [Fact]
    public void JwtSettingsValidation_WithShortSecret_ThrowsOptionsValidationException()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", "TooShortSecretKey" },
            { "JwtSettings:Issuer", "ConnectApi" },
            { "JwtSettings:Audience", "ConnectClient" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();

        // Act & Assert
        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JwtSettings>>().Value);
        Assert.Contains("JwtSettings:Secret", ex.Message);
    }

    [Fact]
    public void JwtSettingsValidation_WithValidSecret_Succeeds()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", ValidSecret },
            { "JwtSettings:Issuer", "ConnectApi" },
            { "JwtSettings:Audience", "ConnectClient" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<JwtSettings>>().Value;

        // Assert
        Assert.Equal(ValidSecret, options.Secret);
        Assert.Equal("ConnectApi", options.Issuer);
    }
}
