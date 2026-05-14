namespace ClaimManager.Domain.Audit;

public sealed class IntegrationHealthIncident
{
    public Guid Id { get; set; }

    public string BoundaryName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }
}