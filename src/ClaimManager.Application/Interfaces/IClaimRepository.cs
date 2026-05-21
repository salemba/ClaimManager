namespace ClaimManager.Application.Interfaces;

using ClaimManager.Domain.Claims.Entities;

public interface IClaimRepository
{
    Task<Claim?> GetByIdAsync(Guid id);
    Task UpdateAsync(Claim claim);
}
