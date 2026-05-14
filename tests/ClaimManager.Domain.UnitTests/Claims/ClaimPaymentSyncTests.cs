using ClaimManager.Domain.Claims;

namespace ClaimManager.Domain.UnitTests.Claims;

public sealed class ClaimPaymentSyncTests
{
    [Fact]
    public void ApplyPaymentData_sets_all_payment_fields_and_returns_audit_summary()
    {
        var claim = CreateClaim("CLM-0300", "POL-0200");
        var settledAt = DateTimeOffset.UtcNow.AddHours(-3);
        var syncedAt = DateTime.UtcNow.AddSeconds(-1);

        var auditSummary = claim.ApplyPaymentData(
            " PAY-123 ",
            " Settled ",
            152.45m,
            " USD ",
            settledAt,
            syncedAt);

        Assert.Equal("PAY-123", claim.PaymentReference);
        Assert.Equal("Settled", claim.PaymentStatus);
        Assert.Equal(152.45m, claim.PaymentAmount);
        Assert.Equal("USD", claim.PaymentCurrency);
        Assert.Equal(settledAt, claim.PaymentSettledAt);
        Assert.Equal(syncedAt, claim.PaymentSyncedAtUtc);
        Assert.NotEmpty(auditSummary);
    }

    [Fact]
    public void ApplyPaymentData_with_null_record_sets_synced_timestamp_and_returns_no_payment_summary()
    {
        var claim = CreateClaim("CLM-0301", "POL-0201");
        var syncedAt = DateTime.UtcNow.AddSeconds(-1);

        var auditSummary = claim.ApplyPaymentData(null, null, null, null, null, syncedAt);

        Assert.Null(claim.PaymentReference);
        Assert.Null(claim.PaymentStatus);
        Assert.Null(claim.PaymentAmount);
        Assert.Null(claim.PaymentCurrency);
        Assert.Null(claim.PaymentSettledAt);
        Assert.Equal(syncedAt, claim.PaymentSyncedAtUtc);
        Assert.Equal("Payment data synchronized — no active payment on file.", auditSummary);
    }

    [Fact]
    public void ApplyPaymentData_clears_payment_data_integrity_warning_and_notes_it_in_summary()
    {
        var claim = CreateClaim("CLM-0302", "POL-0202");
        claim.HasDataIntegrityWarning = true;
        claim.DataIntegrityWarningMessage = "Payment data synchronization failed — previous failure.";

        var auditSummary = claim.ApplyPaymentData(
            "PAY-456",
            "Pending",
            90.10m,
            "USD",
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddSeconds(-1));

        Assert.False(claim.HasDataIntegrityWarning);
        Assert.Null(claim.DataIntegrityWarningMessage);
        Assert.Contains("warning cleared", auditSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyPaymentData_does_not_clear_non_payment_data_integrity_warning()
    {
        var claim = CreateClaim("CLM-0303", "POL-0203");
        claim.HasDataIntegrityWarning = true;
        claim.DataIntegrityWarningMessage = "External document repository unreachable.";

        claim.ApplyPaymentData(
            "PAY-789",
            "Pending",
            45.50m,
            "USD",
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddSeconds(-1));

        Assert.True(claim.HasDataIntegrityWarning);
        Assert.Equal("External document repository unreachable.", claim.DataIntegrityWarningMessage);
    }

    [Fact]
    public void MarkPaymentSyncFailed_sets_data_integrity_warning_with_reason()
    {
        var claim = CreateClaim("CLM-0304", "POL-0204");

        claim.MarkPaymentSyncFailed("reason");

        Assert.True(claim.HasDataIntegrityWarning);
        Assert.Equal("Payment data synchronization failed — reason", claim.DataIntegrityWarningMessage);
    }

    [Fact]
    public void MarkPaymentSyncFailed_caps_message_at_500_characters()
    {
        var claim = CreateClaim("CLM-0305", "POL-0205");

        claim.MarkPaymentSyncFailed(new string('x', 600));

        Assert.NotNull(claim.DataIntegrityWarningMessage);
        Assert.True(claim.DataIntegrityWarningMessage.Length <= 500);
        Assert.StartsWith("Payment data synchronization failed — ", claim.DataIntegrityWarningMessage, StringComparison.Ordinal);
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