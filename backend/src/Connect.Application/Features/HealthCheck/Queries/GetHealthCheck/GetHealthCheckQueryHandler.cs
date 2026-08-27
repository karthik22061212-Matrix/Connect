using Connect.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Connect.Application.Features.HealthCheck.Queries.GetHealthCheck;

public class GetHealthCheckQueryHandler : IRequestHandler<GetHealthCheckQuery, HealthCheckDto>
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IApplicationDbContext _context;
    private readonly IHostEnvironment _environment;

    public GetHealthCheckQueryHandler(
        IDateTimeProvider dateTimeProvider,
        IApplicationDbContext context,
        IHostEnvironment environment)
    {
        _dateTimeProvider = dateTimeProvider;
        _context = context;
        _environment = environment;
    }

    public async Task<HealthCheckDto> Handle(GetHealthCheckQuery request, CancellationToken cancellationToken)
    {
        bool dbConnected = false;
        string dbHost = string.Empty;

        try
        {
            dbConnected = await _context.Database.CanConnectAsync(cancellationToken);
            var connStr = _context.Database.GetConnectionString();
            if (!string.IsNullOrEmpty(connStr))
            {
                var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connStr };
                if (builder.TryGetValue("Server", out var serverVal) || builder.TryGetValue("Data Source", out serverVal))
                {
                    dbHost = serverVal?.ToString() ?? string.Empty;
                }
            }
        }
        catch
        {
            dbConnected = false;
        }

        return new HealthCheckDto
        {
            Status = dbConnected ? "Healthy" : "Unhealthy",
            Timestamp = _dateTimeProvider.UtcNow,
            Service = "Connect API",
            Environment = _environment.EnvironmentName,
            DatabaseHost = dbHost,
            DatabaseConnected = dbConnected
        };
    }
}
