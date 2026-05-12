namespace ClaimManager.Application.Security;

public static class ClaimManagerRoles
{
	public const string Adjuster = "adjuster";
	public const string Supervisor = "supervisor";
	public const string Governance = "governance";
	public const string Admin = "admin";

	public static readonly string[] All =
	[
		Adjuster,
		Supervisor,
		Governance,
		Admin
	];
}
