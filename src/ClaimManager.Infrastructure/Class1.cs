namespace ClaimManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;

public sealed class ClaimManagerUser : IdentityUser<Guid>
{
	public DateTime CreatedAtUtc { get; set; }
}
