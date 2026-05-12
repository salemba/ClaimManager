namespace ClaimManager.Infrastructure.Persistence;

using ClaimManager.Domain.Claims;
using ClaimManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public sealed class ClaimManagerDbContext(DbContextOptions<ClaimManagerDbContext> options)
    : IdentityDbContext<ClaimManagerUser, ClaimManagerRole, Guid>(options)
{
    public DbSet<Claim> Claims => Set<Claim>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ClaimManagerUser>().ToTable("users");
        builder.Entity<ClaimManagerRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<Claim>(entity =>
        {
            entity.ToTable("claims");
            entity.HasKey(claim => claim.Id).HasName("pk_claims");

            entity.Property(claim => claim.Id).HasColumnName("id");
            entity.Property(claim => claim.ClaimNumber)
                .HasColumnName("claim_number")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(claim => claim.Status)
                .HasColumnName("status")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(claim => claim.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();

            entity.HasIndex(claim => claim.ClaimNumber)
                .IsUnique()
                .HasDatabaseName("ix_claims_claim_number");
        });

        builder.Entity<ClaimManagerRole>().HasData(ClaimManagerSeedData.Roles);
        builder.Entity<ClaimManagerUser>().HasData(ClaimManagerSeedData.Users);
        builder.Entity<IdentityUserRole<Guid>>().HasData(ClaimManagerSeedData.UserRoles);
        builder.Entity<Claim>().HasData(ClaimManagerSeedData.Claims);
    }
}