namespace ClaimManager.Infrastructure.Integrations.DocumentRepository;

public sealed class LocalDocumentRepository(string? storageRootPath = null) : IDocumentRepository
{
    private readonly string _storageRootPath = string.IsNullOrWhiteSpace(storageRootPath)
        ? Path.Combine(Path.GetTempPath(), "claimmanager", "documents")
        : storageRootPath;

    public async Task<StoredClaimDocument> SaveAsync(DocumentRepositorySaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fileName = request.FileName.Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Value is required.", nameof(request));
        }

        if (request.Content.Length == 0)
        {
            throw new ArgumentException("Content is required.", nameof(request));
        }

        Directory.CreateDirectory(_storageRootPath);

        var extension = Path.GetExtension(fileName).Trim().ToLowerInvariant();
        var storageFileName = $"{Guid.NewGuid():N}{extension}";
        var storagePath = Path.Combine(_storageRootPath, storageFileName);

        await File.WriteAllBytesAsync(storagePath, request.Content, cancellationToken);

        return new StoredClaimDocument(
            fileName,
            extension,
            storageFileName,
            string.IsNullOrWhiteSpace(request.ContentType) ? null : request.ContentType.Trim(),
            request.Content.LongLength);
    }

    public Task DeleteAsync(string storageIdentifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedStorageIdentifier = storageIdentifier.Trim();
        if (string.IsNullOrWhiteSpace(normalizedStorageIdentifier))
        {
            throw new ArgumentException("Value is required.", nameof(storageIdentifier));
        }

        var storagePath = Path.Combine(_storageRootPath, normalizedStorageIdentifier);
        if (File.Exists(storagePath))
        {
            File.Delete(storagePath);
        }

        return Task.CompletedTask;
    }
}