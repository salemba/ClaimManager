namespace ClaimManager.Application.Common.Interfaces;

using ClaimManager.Domain.Claims;
using Microsoft.EntityFrameworkCore;

public interface IApplicationDbContext
{
    DbSet<Claim> Claims { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
