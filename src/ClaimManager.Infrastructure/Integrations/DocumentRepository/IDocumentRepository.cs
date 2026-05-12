namespace ClaimManager.Infrastructure.Integrations.DocumentRepository;

public interface IDocumentRepository
{
    Task<StoredClaimDocument> SaveAsync(DocumentRepositorySaveRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(string storageIdentifier, CancellationToken cancellationToken);
}