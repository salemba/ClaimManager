namespace ClaimManager.Domain.Claims;

public sealed class Claim
{
	public Guid Id { get; set; }

	public string ClaimNumber { get; set; } = string.Empty;

	public string Status { get; set; } = string.Empty;

	public DateTime CreatedAtUtc { get; set; }
}
