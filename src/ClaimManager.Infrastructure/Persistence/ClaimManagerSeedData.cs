namespace ClaimManager.Infrastructure.Persistence;

using ClaimManager.Application.Security;
using ClaimManager.Domain.Claims;
using ClaimManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

internal static class ClaimManagerSeedData
{
    public static readonly Guid AdjusterRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SupervisorRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid GovernanceRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid AdminRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static readonly Guid AdjusterUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid AdminUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid SupervisorUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid InitialClaimId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static readonly DateTime SeededAtUtc = new(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc);

    private const string AdjusterPasswordHash = "AQAAAAIAAYagAAAAEMdH9tx3PLIMnRe1anrutoPiJAxbIVu/jECUmicySf7/3Ao5BaeZdL54Nz0vGpUCUQ==";
    private const string AdminPasswordHash = "AQAAAAIAAYagAAAAEH3q/PpAXd3zfeLIL5H8+XXWYNLNN00eEwnyf4ozBrODEhWBARTAeZytSIsO3I1C3Q==";
    private const string SupervisorPasswordHash = "AQAAAAIAAYagAAAAEOUuFEwF4KN1J99pZOcA/NhSulx4W7w+YJV/mk8bRg6lI2sxfCR7TK2uiq/i95CuMA==";

    public static IReadOnlyList<ClaimManagerRole> Roles =>
    [
        CreateRole(AdjusterRoleId, ClaimManagerRoles.Adjuster),
        CreateRole(SupervisorRoleId, ClaimManagerRoles.Supervisor),
        CreateRole(GovernanceRoleId, ClaimManagerRoles.Governance),
        CreateRole(AdminRoleId, ClaimManagerRoles.Admin)
    ];

    public static IReadOnlyList<ClaimManagerUser> Users =>
    [
        CreateUser(AdjusterUserId, "adjuster@claimmanager.local", AdjusterPasswordHash),
        CreateUser(AdminUserId, "admin@claimmanager.local", AdminPasswordHash),
        CreateUser(SupervisorUserId, "supervisor@claimmanager.local", SupervisorPasswordHash)
    ];

    public static IReadOnlyList<IdentityUserRole<Guid>> UserRoles =>
    [
        new()
        {
            UserId = AdjusterUserId,
            RoleId = AdjusterRoleId
        },
        new()
        {
            UserId = AdminUserId,
            RoleId = AdminRoleId
        },
        new()
        {
            UserId = SupervisorUserId,
            RoleId = SupervisorRoleId
        }
    ];

    public static IReadOnlyList<Claim> Claims =>
    [
        new()
        {
            Id = InitialClaimId,
            ClaimNumber = "CLM-0001",
            Status = "new",
            ClaimantName = "Jordan Avery",
            ClaimantEmail = "jordan.avery@example.com",
            ClaimantPhone = "555-0100",
            PolicyNumber = "POL-2026-0001",
            LossDateUtc = SeededAtUtc.AddDays(-3),
            LossType = "Water damage",
            LossDescription = "Kitchen pipe burst caused water damage across the lower level.",
            CreatedAtUtc = SeededAtUtc,
            CreatedByUserId = AdjusterUserId.ToString()
        }
    ];

    private static ClaimManagerRole CreateRole(Guid id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ConcurrencyStamp = $"role-{id}"
        };

    private static ClaimManagerUser CreateUser(Guid id, string email, string passwordHash)
    {
        return new ClaimManagerUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            CreatedAtUtc = SeededAtUtc,
            SecurityStamp = $"security-{id}",
            ConcurrencyStamp = $"user-{id}",
            PasswordHash = passwordHash
        };
    }
}