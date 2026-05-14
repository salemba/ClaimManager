namespace ClaimManager.Infrastructure.Integrations.DocumentRepository;

public interface IDocumentRepository
{
    Task<IReadOnlyList<StoredClaimDocument>> GetDocumentListAsync(string claimNumber, CancellationToken cancellationToken);

    Task<StoredClaimDocument> SaveAsync(DocumentRepositorySaveRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(string storageIdentifier, CancellationToken cancellationToken);
}