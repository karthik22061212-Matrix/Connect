using System.Text;
using Connect.Application.Common.Interfaces;
using Connect.Infrastructure.Identity;
using Connect.Application.Common.Diagnostics;
using Connect.Infrastructure.Diagnostics;
using Connect.Infrastructure.Persistence;
using Connect.Infrastructure.Persistence.Repositories;
using Connect.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Connect.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration["SQLAZURECONNSTR_DefaultConnection"]
                ?? configuration["SQLCONNSTR_DefaultConnection"]
                ?? configuration["ConnectionStrings__DefaultConnection"];
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string 'DefaultConnection' not found.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IPresenceTracker, Realtime.PresenceTracker>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDiagnosticLogService, InMemoryDiagnosticLogService>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IPushNotificationService, Notifications.FcmPushNotificationService>();
        services.AddScoped<ICallTimeoutProcessor, CallTimeoutProcessor>();
        services.AddScoped<IPresenceVisibilityService, PresenceVisibilityService>();

        services.AddHostedService<CallHistoryPurgeBackgroundService>();
        services.AddHostedService<ExpiredAccountsPurgeBackgroundService>();
        services.AddHostedService<ExpiredRefreshTokensPurgeBackgroundService>();
        services.AddHostedService<CallTimeoutBackgroundService>();

        services.AddSignalR();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<Connect.Infrastructure.Configuration.TurnSettings>()
            .Bind(configuration.GetSection(Connect.Infrastructure.Configuration.TurnSettings.SectionName))
            .ValidateOnStart();

        services.AddScoped<ITurnCredentialService, TurnCredentialService>();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwtSettingsOptions) =>
            {
                var jwtSettings = jwtSettingsOptions.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ClockSkew = TimeSpan.Zero
                };

            });

        services.AddAuthentication(defaultScheme: JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        return services;
    }
}

