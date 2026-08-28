namespace Connect.Application.Features.Calls.Models;

public record RecordNetworkRestoredResultDto(
    Guid CallId,
    Guid CallerId,
    Guid CalleeId,
    Guid OtherUserId
);
