using ClaimManager.Infrastructure.Integrations.DocumentRepository;

namespace ClaimManager.Infrastructure.IntegrationTests.Persistence;

public sealed class LocalDocumentRepositoryTests : IDisposable
{
    private readonly string _storageRootPath = Path.Combine(Path.GetTempPath(), "claimmanager-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_persists_bytes_with_safe_storage_identifier_and_returns_metadata()
    {
        var repository = new LocalDocumentRepository(_storageRootPath);

        var storedDocument = await repository.SaveAsync(
            new DocumentRepositorySaveRequest("estimate.pdf", [1, 2, 3, 4], "application/pdf"),
            CancellationToken.None);

        var persistedPath = Path.Combine(_storageRootPath, storedDocument.StorageIdentifier);

        Assert.Equal("estimate.pdf", storedDocument.FileName);
        Assert.Equal(".pdf", storedDocument.FileType);
        Assert.Equal("application/pdf", storedDocument.ContentType);
        Assert.Equal(4, storedDocument.FileSizeBytes);
        Assert.NotEqual("estimate.pdf", storedDocument.StorageIdentifier);
        Assert.True(File.Exists(persistedPath));
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(persistedPath));
    }

    [Fact]
    public async Task DeleteAsync_removes_a_previously_stored_document()
    {
        var repository = new LocalDocumentRepository(_storageRootPath);
        var storedDocument = await repository.SaveAsync(
            new DocumentRepositorySaveRequest("estimate.pdf", [1, 2, 3, 4], "application/pdf"),
            CancellationToken.None);

        var persistedPath = Path.Combine(_storageRootPath, storedDocument.StorageIdentifier);
        Assert.True(File.Exists(persistedPath));

        await repository.DeleteAsync(storedDocument.StorageIdentifier, CancellationToken.None);

        Assert.False(File.Exists(persistedPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRootPath))
        {
            Directory.Delete(_storageRootPath, recursive: true);
        }
    }
}