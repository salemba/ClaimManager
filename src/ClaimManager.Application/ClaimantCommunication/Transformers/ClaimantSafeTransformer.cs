namespace ClaimManager.Application.ClaimantCommunication.Transformers;

public static class ClaimantSafeTransformer
{
    // Epic 4 will add generation, review, and governance here.
    // Story 2.5 accepts already-approved claimant-safe content as-is.
    public static (string Subject, string Body) Transform(string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Subject is required for claimant-safe transformation.", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Body is required for claimant-safe transformation.", nameof(body));
        }

        return (subject.Trim(), body.Trim());
    }
}
