namespace ClaimManager.Application.Claims.Commands;

public sealed record UpdateClaimCommand(
    Guid Id,
    string ClaimantName,
    string ClaimantEmail,
    string ClaimantPhone,
    string PolicyNumber,
    DateTime LossDateUtc,
    string LossType,
    string LossDescription);