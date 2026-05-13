namespace ClaimManager.Infrastructure.Integrations.DocumentRepository;

public sealed class DocumentRepositoryOptions
{
    public string? BaseUrl { get; init; }
    public string? ApiKey { get; init; }
}