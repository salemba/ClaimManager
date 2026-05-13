using ClaimManager.Domain.Claims;

namespace ClaimManager.Application.UnitTests.Claims;

public sealed class ClaimWorkflowTests
{
    private static Claim CreateNewClaim() =>
        Claim.Create(
            "CLM-TEST",
            "Jordan Avery",
            "jordan.avery@example.com",
            "555-0100",
            "POL-0001",
            DateTime.UtcNow.AddDays(-3),
            "Water damage",
            "Pipe burst in kitchen.",
            "user-001",
            DateTime.UtcNow.AddDays(-3));

    // ---- AdvanceWorkflow: valid transitions ----

    [Fact]
    public void AdvanceWorkflow_from_new_moves_to_open_and_sets_fields()
    {
        var claim = CreateNewClaim();

        var summary = claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow);

        Assert.Equal("open", claim.Status);
        Assert.Equal("adjuster-001", claim.OwnedByUserId);
        Assert.Equal("Investigate loss details", claim.NextExpectedAction);
        Assert.Equal("adjuster-001", claim.UpdatedByUserId);
        Assert.NotNull(claim.UpdatedAtUtc);
        Assert.Null(claim.BlockerType);
        Assert.Null(claim.BlockerReason);
        Assert.Contains("new", summary);
        Assert.Contains("open", summary);
    }

    [Fact]
    public void AdvanceWorkflow_from_open_moves_to_in_review_and_sets_fields()
    {
        var claim = CreateNewClaim();
        claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow);

        var summary = claim.AdvanceWorkflow("adjuster-002", DateTime.UtcNow);

        Assert.Equal("in-review", claim.Status);
        Assert.Equal("adjuster-002", claim.OwnedByUserId);
        Assert.Equal("Review and document findings", claim.NextExpectedAction);
        Assert.Equal("adjuster-002", claim.UpdatedByUserId);
        Assert.NotNull(claim.UpdatedAtUtc);
        Assert.Null(claim.BlockerType);
        Assert.Null(claim.BlockerReason);
        Assert.Contains("open", summary);
        Assert.Contains("in-review", summary);
    }

    [Fact]
    public void AdvanceWorkflow_from_approved_moves_to_closed_and_clears_next_action()
    {
        var claim = CreateNewClaim();
        // Manually set to approved for this test
        claim.Status = "approved";

        var summary = claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow);

        Assert.Equal("closed", claim.Status);
        Assert.Equal("adjuster-001", claim.OwnedByUserId);
        Assert.Null(claim.NextExpectedAction);
        Assert.Contains("approved", summary);
        Assert.Contains("closed", summary);
    }

    // ---- AdvanceWorkflow: clears existing blocker ----

    [Fact]
    public void AdvanceWorkflow_clears_blocker_fields_from_previous_state()
    {
        var claim = CreateNewClaim();
        // Simulate a claim that had a blocker set
        claim.BlockerType = "awaiting-information";
        claim.BlockerReason = "Missing police report";

        claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow);

        Assert.Null(claim.BlockerType);
        Assert.Null(claim.BlockerReason);
    }

    // ---- AdvanceWorkflow: invalid transitions throw ----

    [Fact]
    public void AdvanceWorkflow_from_in_review_throws_InvalidOperationException()
    {
        var claim = CreateNewClaim();
        claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow);
        claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow); // now in-review

        Assert.Throws<InvalidOperationException>(() => claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow));
    }

    [Fact]
    public void AdvanceWorkflow_from_pending_throws_InvalidOperationException()
    {
        var claim = CreateNewClaim();
        claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow); // new → open
        claim.RouteForPaymentApproval("Need supervisor sign-off on this amount.", "adjuster-001", DateTime.UtcNow); // open → pending

        Assert.Throws<InvalidOperationException>(() => claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow));
    }

    [Fact]
    public void AdvanceWorkflow_from_closed_throws_InvalidOperationException()
    {
        var claim = CreateNewClaim();
        claim.Status = "approved";
        claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow); // approved → closed

        Assert.Throws<InvalidOperationException>(() => claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow));
    }

    // ---- RouteForPaymentApproval: valid transitions ----

    [Fact]
    public void RouteForPaymentApproval_from_open_sets_pending_state_and_blocker()
    {
        var claim = CreateNewClaim();
        claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow); // new → open
        const string rationale = "Payment exceeds standard threshold, requires supervisor review.";

        claim.RouteForPaymentApproval(rationale, "adjuster-001", DateTime.UtcNow);

        Assert.Equal("pending", claim.Status);
        Assert.Equal("awaiting-payment-approval", claim.BlockerType);
        Assert.Equal(rationale, claim.BlockerReason);
        Assert.Equal("Awaiting payment approval decision", claim.NextExpectedAction);
        Assert.Equal("adjuster-001", claim.OwnedByUserId);
        Assert.Equal("adjuster-001", claim.UpdatedByUserId);
        Assert.NotNull(claim.UpdatedAtUtc);
    }

    [Fact]
    public void RouteForPaymentApproval_from_in_review_sets_pending_state_and_blocker()
    {
        var claim = CreateNewClaim();
        claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow); // new → open
        claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow); // open → in-review
        const string rationale = "Payment exceeds standard threshold, requires supervisor review.";

        claim.RouteForPaymentApproval(rationale, "adjuster-002", DateTime.UtcNow);

        Assert.Equal("pending", claim.Status);
        Assert.Equal("awaiting-payment-approval", claim.BlockerType);
        Assert.Equal(rationale, claim.BlockerReason);
        Assert.Equal("Awaiting payment approval decision", claim.NextExpectedAction);
        Assert.Equal("adjuster-002", claim.OwnedByUserId);
        Assert.Equal("adjuster-002", claim.UpdatedByUserId);
        Assert.NotNull(claim.UpdatedAtUtc);
    }

    // ---- RouteForPaymentApproval: invalid transitions throw ----

    [Fact]
    public void RouteForPaymentApproval_from_new_throws_InvalidOperationException()
    {
        var claim = CreateNewClaim();

        Assert.Throws<InvalidOperationException>(() =>
            claim.RouteForPaymentApproval("Valid rationale here.", "adjuster-001", DateTime.UtcNow));
    }

    [Fact]
    public void RouteForPaymentApproval_from_pending_throws_InvalidOperationException()
    {
        var claim = CreateNewClaim();
        claim.AdvanceWorkflow("adjuster-001", DateTime.UtcNow);
        claim.RouteForPaymentApproval("Initial routing.", "adjuster-001", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            claim.RouteForPaymentApproval("Second routing attempt.", "adjuster-001", DateTime.UtcNow));
    }

    [Fact]
    public void RouteForPaymentApproval_from_closed_throws_InvalidOperationException()
    {
        var claim = CreateNewClaim();
        claim.Status = "closed";

        Assert.Throws<InvalidOperationException>(() =>
            claim.RouteForPaymentApproval("Valid rationale here.", "adjuster-001", DateTime.UtcNow));
    }
}
