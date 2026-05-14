using ClaimManager.Domain.Claims;

namespace ClaimManager.Domain.UnitTests.Claims;

public sealed class ClaimPolicySyncTests
{
    [Fact]
    public void ApplyPolicyData_sets_all_policy_fields_and_returns_audit_summary()
    {
        var claim = Claim.Create(
            "CLM-0200",
            "Test User",
            "t@example.com",
            "555-0000",
            "POL-0099",
            DateTime.UtcNow.AddDays(-1),
            "Fire",
            "Basement fire.",
            "adjuster-1",
            DateTime.UtcNow.AddMinutes(-1));

        var syncedAt = DateTime.UtcNow.AddSeconds(-1);

        var auditSummary = claim.ApplyPolicyData(
            " Jane Doe ",
            " Auto ",
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 12, 31),
            syncedAt);

        Assert.Equal("Jane Doe", claim.PolicyHolder);
        Assert.Equal("Auto", claim.CoverageType);
        Assert.Equal(new DateOnly(2025, 1, 1), claim.PolicyEffectiveDate);
        Assert.Equal(new DateOnly(2026, 12, 31), claim.PolicyExpirationDate);
        Assert.Equal(syncedAt, claim.PolicySyncedAtUtc);
        Assert.NotEmpty(auditSummary);
    }

    [Fact]
    public void ApplyPolicyData_clears_policy_related_data_integrity_warning_and_notes_it_in_summary()
    {
        var claim = Claim.Create(
            "CLM-0201",
            "Test User",
            "t@example.com",
            "555-0000",
            "POL-0100",
            DateTime.UtcNow.AddDays(-1),
            "Fire",
            "Basement fire.",
            "adjuster-1",
            DateTime.UtcNow.AddMinutes(-1));

        claim.HasDataIntegrityWarning = true;
        claim.DataIntegrityWarningMessage = "Policy data synchronization failed — previous failure.";

        var auditSummary = claim.ApplyPolicyData(
            "Jane Doe",
            "Auto",
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 12, 31),
            DateTime.UtcNow.AddSeconds(-1));

        Assert.False(claim.HasDataIntegrityWarning);
        Assert.Null(claim.DataIntegrityWarningMessage);
        Assert.Contains("warning cleared", auditSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyPolicyData_does_not_clear_non_policy_data_integrity_warning()
    {
        var claim = Claim.Create(
            "CLM-0202",
            "Test User",
            "t@example.com",
            "555-0000",
            "POL-0101",
            DateTime.UtcNow.AddDays(-1),
            "Fire",
            "Basement fire.",
            "adjuster-1",
            DateTime.UtcNow.AddMinutes(-1));

        claim.HasDataIntegrityWarning = true;
        claim.DataIntegrityWarningMessage = "External document repository unreachable.";

        claim.ApplyPolicyData(
            "Jane Doe",
            "Auto",
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 12, 31),
            DateTime.UtcNow.AddSeconds(-1));

        Assert.True(claim.HasDataIntegrityWarning);
        Assert.Equal("External document repository unreachable.", claim.DataIntegrityWarningMessage);
    }

    [Fact]
    public void MarkPolicySyncFailed_sets_data_integrity_warning_with_reason()
    {
        var claim = Claim.Create(
            "CLM-0203",
            "Test User",
            "t@example.com",
            "555-0000",
            "POL-0102",
            DateTime.UtcNow.AddDays(-1),
            "Fire",
            "Basement fire.",
            "adjuster-1",
            DateTime.UtcNow.AddMinutes(-1));

        claim.MarkPolicySyncFailed("reason");

        Assert.True(claim.HasDataIntegrityWarning);
        Assert.Equal("Policy data synchronization failed — reason", claim.DataIntegrityWarningMessage);
    }

    [Fact]
    public void MarkPolicySyncFailed_caps_message_at_500_characters()
    {
        var claim = Claim.Create(
            "CLM-0204",
            "Test User",
            "t@example.com",
            "555-0000",
            "POL-0103",
            DateTime.UtcNow.AddDays(-1),
            "Fire",
            "Basement fire.",
            "adjuster-1",
            DateTime.UtcNow.AddMinutes(-1));

        claim.MarkPolicySyncFailed(new string('x', 600));

        Assert.NotNull(claim.DataIntegrityWarningMessage);
        Assert.True(claim.DataIntegrityWarningMessage.Length <= 500);
        Assert.StartsWith("Policy data synchronization failed — ", claim.DataIntegrityWarningMessage, StringComparison.Ordinal);
    }
}