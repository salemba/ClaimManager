namespace ClaimManager.Domain.ClaimantCommunication;

public sealed class ClaimCommunication
{
    public Guid Id { get; set; }

    public Guid ClaimId { get; set; }

    public string CommunicationType { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string Status { get; set; } = "pending";

    public int AttemptCount { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }

    public string? DeliveryId { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public static ClaimCommunication Create(
        Guid claimId,
        string communicationType,
        string channel,
        string recipient,
        string subject,
        string body,
        string? correlationId,
        string createdByUserId,
        DateTime createdAtUtc)
    {
        return new ClaimCommunication
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            CommunicationType = Normalize(communicationType, nameof(communicationType)),
            Channel = Normalize(channel, nameof(channel)),
            Recipient = Normalize(recipient, nameof(recipient)),
            Subject = Normalize(subject, nameof(subject)),
            Body = NormalizeBody(body),
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            Status = "pending",
            AttemptCount = 0,
            CreatedByUserId = Normalize(createdByUserId, nameof(createdByUserId)),
            CreatedAtUtc = EnsureUtc(createdAtUtc)
        };
    }

    public void RecordSent(string deliveryId, DateTime attemptAtUtc)
    {
        Status = "sent";
        DeliveryId = deliveryId?.Trim();
        FailureReason = null;
        AttemptCount++;
        LastAttemptAtUtc = EnsureUtc(attemptAtUtc);
    }

    public void RecordFailed(string failureReason, DateTime attemptAtUtc)
    {
        var normalized = string.IsNullOrWhiteSpace(failureReason) ? "reason unknown" : failureReason.Trim();
        Status = "failed";
        FailureReason = normalized.Length > 500 ? normalized[..500] : normalized;
        AttemptCount++;
        LastAttemptAtUtc = EnsureUtc(attemptAtUtc);
    }

    public bool IsRetryEligible() => Status == "failed";

    public void PrepareRetry()
    {
        if (!IsRetryEligible())
        {
            throw new InvalidOperationException($"Cannot retry a notification in '{Status}' state.");
        }

        Status = "pending";
    }

    private static string Normalize(string value, string paramName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        return normalized;
    }

    private static string NormalizeBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Value is required.", nameof(body));
        }

        return body.Trim();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
