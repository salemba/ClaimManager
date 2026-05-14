namespace ClaimManager.Domain.Claims;

public sealed class ClaimDocument
{
    public Guid Id { get; set; }

    public Guid ClaimId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    public string StorageIdentifier { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;

    public string Source { get; set; } = "uploaded";
}