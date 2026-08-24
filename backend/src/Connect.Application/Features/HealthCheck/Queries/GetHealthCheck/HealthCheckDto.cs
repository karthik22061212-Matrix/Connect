namespace Connect.Application.Features.HealthCheck.Queries.GetHealthCheck;

public class HealthCheckDto
{
    public string Status { get; set; } = "Healthy";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Service { get; set; } = "Connect API";
    public string Environment { get; set; } = "LocalDB / Dev Stack";
}
