namespace ClaimManager.Application.Exceptions;

public class ForceReassignmentNotAllowedException : Exception
{
    public ForceReassignmentNotAllowedException(string message) : base(message)
    {
    }
}
