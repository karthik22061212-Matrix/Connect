using System.ComponentModel.DataAnnotations;

namespace Connect.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    [Required(AllowEmptyStrings = false, ErrorMessage = "JwtSettings:Secret is required.")]
    [MinLength(32, ErrorMessage = "JwtSettings:Secret must be at least 32 characters long.")]
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "ConnectApi";

    public string Audience { get; set; } = "ConnectClient";

    public int ExpiryMinutes { get; set; } = 60;

    public int RefreshTokenExpiryDays { get; set; } = 30;
}
