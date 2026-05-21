namespace ClaimManager.Application.Interfaces;

using ClaimManager.Domain.Audit.Entities;

public interface IAuditRepository
{
    Task AddForceReassignAuditAsync(ForceReassignAudit audit);
}
