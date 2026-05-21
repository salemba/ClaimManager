namespace ClaimManager.Infrastructure.Repositories;

using ClaimManager.Application.Interfaces;
using ClaimManager.Domain.Claims.Entities;

public class LocalClaimRepository : IClaimRepository
{
    private static readonly List<Claim> _claims = new();

    public Task<Claim?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(_claims.FirstOrDefault(c => c.Id == id));
    }

    public Task UpdateAsync(Claim claim)
    {
        var index = _claims.FindIndex(c => c.Id == claim.Id);
        if (index != -1)
        {
            _claims[index] = claim;
        }
        else
        {
            _claims.Add(claim);
        }
        return Task.CompletedTask;
    }
}
