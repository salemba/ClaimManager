using ClaimManager.Domain.Claims;

namespace ClaimManager.Domain.UnitTests.Claims;

public sealed class ClaimDocumentSyncTests
{
    [Fact]
    public void ApplyDocumentSync_with_imported_count_returns_import_summary()
    {
        var claim = CreateClaim("CLM-0400", "POL-0300");
        var syncedAt = DateTime.UtcNow.AddSeconds(-1);

        var auditSummary = claim.ApplyDocumentSync(syncedAt, 3);

        Assert.Equal(syncedAt, claim.DocumentSyncedAtUtc);
        Assert.Contains("3 document(s) imported", auditSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyDocumentSync_with_zero_count_returns_no_new_documents_summary()
    {
        var claim = CreateClaim("CLM-0401", "POL-0301");

        var auditSummary = claim.ApplyDocumentSync(DateTime.UtcNow.AddSeconds(-1), 0);

        Assert.Contains("No new documents found", auditSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyDocumentSync_clears_document_data_integrity_warning()
    {
        var claim = CreateClaim("CLM-0402", "POL-0302");
        claim.HasDataIntegrityWarning = true;
        claim.DataIntegrityWarningMessage = "Document data synchronization failed — previous failure.";

        var auditSummary = claim.ApplyDocumentSync(DateTime.UtcNow.AddSeconds(-1), 1);

        Assert.False(claim.HasDataIntegrityWarning);
        Assert.Null(claim.DataIntegrityWarningMessage);
        Assert.Contains("warning cleared", auditSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyDocumentSync_does_not_clear_non_document_data_integrity_warning()
    {
        var claim = CreateClaim("CLM-0403", "POL-0303");
        claim.HasDataIntegrityWarning = true;
        claim.DataIntegrityWarningMessage = "Policy data synchronization failed — previous failure.";

        claim.ApplyDocumentSync(DateTime.UtcNow.AddSeconds(-1), 1);

        Assert.True(claim.HasDataIntegrityWarning);
        Assert.Equal("Policy data synchronization failed — previous failure.", claim.DataIntegrityWarningMessage);
    }

    [Fact]
    public void MarkDocumentSyncFailed_sets_data_integrity_warning_with_reason()
    {
        var claim = CreateClaim("CLM-0404", "POL-0304");

        claim.MarkDocumentSyncFailed("reason");

        Assert.True(claim.HasDataIntegrityWarning);
        Assert.Equal("Document data synchronization failed — reason", claim.DataIntegrityWarningMessage);
    }

    [Fact]
    public void MarkDocumentSyncFailed_caps_message_at_500_characters()
    {
        var claim = CreateClaim("CLM-0405", "POL-0305");

        claim.MarkDocumentSyncFailed(new string('x', 600));

        Assert.NotNull(claim.DataIntegrityWarningMessage);
        Assert.True(claim.DataIntegrityWarningMessage.Length <= 500);
        Assert.StartsWith("Document data synchronization failed — ", claim.DataIntegrityWarningMessage, StringComparison.Ordinal);
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