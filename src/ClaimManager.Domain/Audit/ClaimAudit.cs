namespace ClaimManager.Domain.Audit;

public sealed class ClaimAudit
{
    public Guid Id { get; set; }

    public Guid ClaimId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTime PerformedAtUtc { get; set; }

    public string PerformedByUserId { get; set; } = string.Empty;
}