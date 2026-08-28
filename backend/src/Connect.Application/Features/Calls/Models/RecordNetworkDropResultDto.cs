namespace Connect.Application.Features.Calls.Models;

public record RecordNetworkDropResultDto(
    Guid CallId,
    Guid CallerId,
    Guid CalleeId,
    Guid OtherUserId
);
