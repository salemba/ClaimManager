using ClaimManager.Application.Security;

namespace ClaimManager.Application.UnitTests.Security;

public sealed class ClaimManagerRolesTests
{
    [Fact]
    public void All_contains_each_supported_role_once()
    {
        Assert.Equal(4, ClaimManagerRoles.All.Length);
        Assert.Equal(4, ClaimManagerRoles.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(ClaimManagerRoles.Adjuster, ClaimManagerRoles.All);
        Assert.Contains(ClaimManagerRoles.Supervisor, ClaimManagerRoles.All);
        Assert.Contains(ClaimManagerRoles.Governance, ClaimManagerRoles.All);
        Assert.Contains(ClaimManagerRoles.Admin, ClaimManagerRoles.All);
    }
}