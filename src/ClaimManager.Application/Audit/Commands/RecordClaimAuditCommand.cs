namespace ClaimManager.Application.Audit.Commands;

using ClaimManager.Domain.Audit;

public sealed record RecordClaimAuditCommand(
    Guid ClaimId,
    string Action,
    string Summary,
    string PerformedByUserId,
    DateTime PerformedAtUtc)
{
    public ClaimAudit ToEntity() =>
        new()
        {
            Id = Guid.NewGuid(),
            ClaimId = ClaimId,
            Action = Action,
            Summary = Summary,
            PerformedByUserId = PerformedByUserId,
            PerformedAtUtc = PerformedAtUtc
        };
}