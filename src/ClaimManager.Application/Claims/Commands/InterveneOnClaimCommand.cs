namespace ClaimManager.Application.Claims.Commands;

public sealed record InterveneOnClaimCommand(
    Guid Id,
    string NewOwnerId,
    string TargetStatus,
    byte[] RowVersion);
