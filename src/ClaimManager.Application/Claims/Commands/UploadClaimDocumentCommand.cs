namespace ClaimManager.Application.Claims.Commands;

public sealed record UploadClaimDocumentCommand(
    string FileName,
    string? ContentType,
    long FileSizeBytes,
    byte[] Content);