using ClaimManager.Domain.Claims;

namespace ClaimManager.Domain.UnitTests.Claims;

public sealed class ClaimTests
{
    [Fact]
    public void Claim_preserves_core_foundation_fields()
    {
        var createdAtUtc = new DateTime(2026, 5, 11, 8, 30, 0, DateTimeKind.Utc);

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-0099",
            Status = "new",
            CreatedAtUtc = createdAtUtc
        };

        Assert.Equal("CLM-0099", claim.ClaimNumber);
        Assert.Equal("new", claim.Status);
        Assert.Equal(createdAtUtc, claim.CreatedAtUtc);
    }
}