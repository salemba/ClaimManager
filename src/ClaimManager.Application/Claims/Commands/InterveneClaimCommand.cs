namespace ClaimManager.Application.Claims.Commands;

public sealed record InterveneClaimCommand(
    Guid Id,
    string? NewAdjusterId,
    string? NewState,
    string Reason,
    byte[] RowVersion);
