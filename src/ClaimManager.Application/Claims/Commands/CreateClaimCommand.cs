namespace ClaimManager.Application.Claims.Commands;

public sealed record CreateClaimCommand(
    string ClaimantName,
    string ClaimantEmail,
    string ClaimantPhone,
    string PolicyNumber,
    DateTime LossDateUtc,
    string LossType,
    string LossDescription);



