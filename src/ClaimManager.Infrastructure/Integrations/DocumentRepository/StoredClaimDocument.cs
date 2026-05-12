namespace ClaimManager.Infrastructure.Integrations.DocumentRepository;

public sealed record StoredClaimDocument(
    string FileName,
    string FileType,
    string StorageIdentifier,
    string? ContentType,
    long FileSizeBytes);