using ClaimManager.Domain.Audit;
using ClaimManager.Domain.ClaimantCommunication;
using ClaimManager.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ClaimManager.Application.Common.Interfaces;

public interface IClaimManagerDbContext
{
    DbSet<Claim> Claims { get; }
    DbSet<ClaimAudit> ClaimAudits { get; }
    DbSet<IntegrationHealthIncident> IntegrationHealthIncidents { get; }
    DbSet<ClaimNote> ClaimNotes { get; }
    DbSet<ClaimDocument> ClaimDocuments { get; }
    DbSet<ClaimCommunication> ClaimCommunications { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
