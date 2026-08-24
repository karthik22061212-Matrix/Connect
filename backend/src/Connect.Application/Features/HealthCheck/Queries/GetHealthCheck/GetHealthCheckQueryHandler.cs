using Connect.Application.Common.Interfaces;
using MediatR;

namespace Connect.Application.Features.HealthCheck.Queries.GetHealthCheck;

public class GetHealthCheckQueryHandler : IRequestHandler<GetHealthCheckQuery, HealthCheckDto>
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetHealthCheckQueryHandler(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<HealthCheckDto> Handle(GetHealthCheckQuery request, CancellationToken cancellationToken)
    {
        var result = new HealthCheckDto
        {
            Status = "Healthy",
            Timestamp = _dateTimeProvider.UtcNow,
            Service = "Connect API",
            Environment = "LocalDB / Dev Stack"
        };

        return Task.FromResult(result);
    }
}
