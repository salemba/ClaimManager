namespace ClaimManager.Application.Common.Interfaces;

using ClaimManager.Domain.Claims;
using Microsoft.EntityFrameworkCore;

public interface IClaimManagerDbContext
{
    DbSet<Claim> Claims { get; }
}