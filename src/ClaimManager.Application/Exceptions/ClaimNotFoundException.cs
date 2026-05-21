namespace ClaimManager.Application.Exceptions;

public class ClaimNotFoundException : Exception
{
    public ClaimNotFoundException(Guid claimId) : base($"Claim with ID '{claimId}' was not found.")
    {
    }
}
