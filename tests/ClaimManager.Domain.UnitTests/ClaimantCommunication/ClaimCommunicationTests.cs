using ClaimManager.Domain.ClaimantCommunication;

namespace ClaimManager.Domain.UnitTests.ClaimantCommunication;

public sealed class ClaimCommunicationTests
{
    private static readonly Guid TestClaimId = Guid.NewGuid();
    private const string UserId = "adjuster-1";

    [Fact]
    public void Create_produces_pending_communication_with_correct_fields()
    {
        var createdAt = DateTime.UtcNow.AddSeconds(-1);

        var comm = ClaimCommunication.Create(
            TestClaimId,
            "operational",
            "email",
            "claimant@example.com",
            "Your claim status",
            "Your claim has been updated.",
            null,
            UserId,
            createdAt);

        Assert.Equal(TestClaimId, comm.ClaimId);
        Assert.Equal("operational", comm.CommunicationType);
        Assert.Equal("email", comm.Channel);
        Assert.Equal("claimant@example.com", comm.Recipient);
        Assert.Equal("Your claim status", comm.Subject);
        Assert.Equal("Your claim has been updated.", comm.Body);
        Assert.Null(comm.CorrelationId);
        Assert.Equal("pending", comm.Status);
        Assert.Equal(0, comm.AttemptCount);
        Assert.Null(comm.LastAttemptAtUtc);
        Assert.Null(comm.DeliveryId);
        Assert.Null(comm.FailureReason);
        Assert.Equal(createdAt, comm.CreatedAtUtc);
        Assert.Equal(UserId, comm.CreatedByUserId);
        Assert.NotEqual(Guid.Empty, comm.Id);
    }

    [Fact]
    public void Create_trims_whitespace_from_string_fields()
    {
        var comm = ClaimCommunication.Create(
            TestClaimId,
            " operational ",
            " email ",
            " a@b.com ",
            " Subject ",
            " Body text ",
            " CORR-1 ",
            " user-1 ",
            DateTime.UtcNow.AddSeconds(-1));

        Assert.Equal("operational", comm.CommunicationType);
        Assert.Equal("email", comm.Channel);
        Assert.Equal("a@b.com", comm.Recipient);
        Assert.Equal("Subject", comm.Subject);
        Assert.Equal("Body text", comm.Body);
        Assert.Equal("CORR-1", comm.CorrelationId);
        Assert.Equal("user-1", comm.CreatedByUserId);
    }

    [Fact]
    public void Create_stores_null_when_correlationId_is_whitespace()
    {
        var comm = ClaimCommunication.Create(
            TestClaimId, "operational", "email", "a@b.com", "S", "B", "   ", UserId,
            DateTime.UtcNow.AddSeconds(-1));

        Assert.Null(comm.CorrelationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_throws_when_communicationType_is_empty(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            ClaimCommunication.Create(TestClaimId, value, "email", "a@b.com", "S", "B", null, UserId, DateTime.UtcNow.AddSeconds(-1)));
    }

    [Fact]
    public void RecordSent_transitions_status_to_sent_and_increments_attempt_count()
    {
        var comm = CreatePendingCommunication();
        var attemptAt = DateTime.UtcNow;

        comm.RecordSent("DELIVERY-123", attemptAt);

        Assert.Equal("sent", comm.Status);
        Assert.Equal("DELIVERY-123", comm.DeliveryId);
        Assert.Equal(1, comm.AttemptCount);
        Assert.Equal(attemptAt, comm.LastAttemptAtUtc);
        Assert.Null(comm.FailureReason);
    }

    [Fact]
    public void RecordFailed_transitions_status_to_failed_and_records_reason()
    {
        var comm = CreatePendingCommunication();
        var attemptAt = DateTime.UtcNow;

        comm.RecordFailed("Mailbox not found.", attemptAt);

        Assert.Equal("failed", comm.Status);
        Assert.Equal("Mailbox not found.", comm.FailureReason);
        Assert.Equal(1, comm.AttemptCount);
        Assert.Equal(attemptAt, comm.LastAttemptAtUtc);
    }

    [Fact]
    public void RecordFailed_uses_default_message_when_reason_is_blank()
    {
        var comm = CreatePendingCommunication();

        comm.RecordFailed("   ", DateTime.UtcNow);

        Assert.Equal("reason unknown", comm.FailureReason);
    }

    [Fact]
    public void RecordFailed_caps_failure_reason_at_500_characters()
    {
        var comm = CreatePendingCommunication();

        comm.RecordFailed(new string('x', 600), DateTime.UtcNow);

        Assert.NotNull(comm.FailureReason);
        Assert.True(comm.FailureReason!.Length <= 500);
    }

    [Fact]
    public void IsRetryEligible_returns_true_only_for_failed_status()
    {
        var pending = CreatePendingCommunication();
        var sent = CreatePendingCommunication();
        sent.RecordSent("DEL-1", DateTime.UtcNow);
        var failed = CreatePendingCommunication();
        failed.RecordFailed("reason", DateTime.UtcNow);

        Assert.False(pending.IsRetryEligible());
        Assert.False(sent.IsRetryEligible());
        Assert.True(failed.IsRetryEligible());
    }

    [Fact]
    public void PrepareRetry_resets_status_to_pending_for_failed_notification()
    {
        var comm = CreatePendingCommunication();
        comm.RecordFailed("Timeout.", DateTime.UtcNow);

        comm.PrepareRetry();

        Assert.Equal("pending", comm.Status);
    }

    [Fact]
    public void PrepareRetry_preserves_prior_attempt_count_and_failure_reason()
    {
        var comm = CreatePendingCommunication();
        comm.RecordFailed("First attempt failed.", DateTime.UtcNow);

        comm.PrepareRetry();

        Assert.Equal(1, comm.AttemptCount);
        Assert.Equal("First attempt failed.", comm.FailureReason);
    }

    [Fact]
    public void PrepareRetry_throws_when_status_is_not_failed()
    {
        var pending = CreatePendingCommunication();
        var sent = CreatePendingCommunication();
        sent.RecordSent("DEL-1", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => pending.PrepareRetry());
        Assert.Throws<InvalidOperationException>(() => sent.PrepareRetry());
    }

    [Fact]
    public void Multiple_retry_cycles_accumulate_attempt_count()
    {
        var comm = CreatePendingCommunication();

        comm.RecordFailed("attempt 1", DateTime.UtcNow);
        comm.PrepareRetry();
        comm.RecordFailed("attempt 2", DateTime.UtcNow);
        comm.PrepareRetry();
        comm.RecordSent("DEL-FINAL", DateTime.UtcNow);

        Assert.Equal(3, comm.AttemptCount);
        Assert.Equal("sent", comm.Status);
        Assert.Equal("DEL-FINAL", comm.DeliveryId);
    }

    private static ClaimCommunication CreatePendingCommunication()
    {
        return ClaimCommunication.Create(
            TestClaimId,
            "operational",
            "email",
            "claimant@example.com",
            "Status update",
            "Your claim has been updated.",
            null,
            UserId,
            DateTime.UtcNow.AddSeconds(-1));
    }
}
