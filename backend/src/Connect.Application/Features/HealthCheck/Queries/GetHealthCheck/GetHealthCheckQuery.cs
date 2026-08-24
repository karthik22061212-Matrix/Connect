using MediatR;

namespace Connect.Application.Features.HealthCheck.Queries.GetHealthCheck;

public record GetHealthCheckQuery : IRequest<HealthCheckDto>;
