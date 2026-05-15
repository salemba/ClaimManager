using ClaimManager.Domain.Claims;

namespace ClaimManager.Domain.UnitTests.Claims;

public sealed class ClaimReconciliationTests
{
    [Fact]
    public void Multiple_sync_failures_are_tracked_without_overwriting_each_other()
    {
        var claim = CreateClaim("CLM-0500", "POL-0400");

        claim.MarkPolicySyncFailed("policy timeout");
        claim.MarkDocumentSyncFailed("repository unavailable");

        var activeIssues = claim.GetActiveDataIntegrityIssues();

        Assert.Equal(2, activeIssues.Count);
        Assert.Contains(activeIssues, issue => issue.Dependency == "policy" && issue.Message.Contains("policy timeout", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(activeIssues, issue => issue.Dependency == "documents" && issue.Message.Contains("repository unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.True(claim.HasDataIntegrityWarning);
        Assert.NotNull(claim.DataIntegrityWarningMessage);
        Assert.Contains("policy", claim.DataIntegrityWarningMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("document", claim.DataIntegrityWarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Successful_recovery_clears_only_the_resolved_dependency_issue()
    {
        var claim = CreateClaim("CLM-0501", "POL-0401");

        claim.MarkPaymentSyncFailed("payment timeout");
        claim.MarkDocumentSyncFailed("repository unavailable");

        var auditSummary = claim.ApplyPaymentData(
            "PAY-990",
            "Settled",
            125.40m,
            "USD",
            DateTimeOffset.UtcNow.AddMinutes(-30),
            DateTime.UtcNow.AddSeconds(-1));

        var activeIssues = claim.GetActiveDataIntegrityIssues();

        Assert.Single(activeIssues);
        Assert.Equal("documents", activeIssues[0].Dependency);
        Assert.True(claim.HasDataIntegrityWarning);
        Assert.NotNull(claim.DataIntegrityWarningMessage);
        Assert.DoesNotContain("payment", claim.DataIntegrityWarningMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("document", claim.DataIntegrityWarningMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resolved", auditSummary, StringComparison.OrdinalIgnoreCase);
    }

    private static Claim CreateClaim(string claimNumber, string policyNumber)
    {
        return Claim.Create(
            claimNumber,
            "Test User",
            "t@example.com",
            "555-0000",
            policyNumber,
            DateTime.UtcNow.AddDays(-1),
            "Fire",
            "Basement fire.",
            "adjuster-1",
            DateTime.UtcNow.AddMinutes(-1));
    }
}