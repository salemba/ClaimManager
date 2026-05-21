namespace ClaimManager.Infrastructure.Repositories;

using ClaimManager.Application.Interfaces;
using ClaimManager.Domain.Audit.Entities;

public class LocalAuditRepository : IAuditRepository
{
    private static readonly List<ForceReassignAudit> _audits = new();

    public Task AddForceReassignAuditAsync(ForceReassignAudit audit)
    {
        _audits.Add(audit);
        return Task.CompletedTask;
    }
}
