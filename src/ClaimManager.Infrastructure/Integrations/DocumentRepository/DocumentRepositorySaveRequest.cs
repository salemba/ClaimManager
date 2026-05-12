namespace ClaimManager.Infrastructure.Integrations.DocumentRepository;

public sealed record DocumentRepositorySaveRequest(string FileName, byte[] Content, string? ContentType = null);