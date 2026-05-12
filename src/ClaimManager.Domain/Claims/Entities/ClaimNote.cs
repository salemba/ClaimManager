namespace ClaimManager.Domain.Claims;

public sealed class ClaimNote
{
    public Guid Id { get; set; }

    public Guid ClaimId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;
}