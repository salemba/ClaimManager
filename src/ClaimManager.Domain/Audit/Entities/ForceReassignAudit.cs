namespace ClaimManager.Domain.Audit.Entities;

using ClaimManager.Domain.Claims.Enums;

public class ForceReassignAudit
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public string SupervisorId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Reasons { get; set; }
    public ClaimStatus StatusBefore { get; set; }
    public ClaimStatus StatusAfter { get; set; }
    public string AdjusterIdBefore { get; set; }
    public string AdjusterIdAfter { get; set; }
}
