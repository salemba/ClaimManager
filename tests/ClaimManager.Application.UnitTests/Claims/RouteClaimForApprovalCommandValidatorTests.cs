using ClaimManager.Application.Claims.Commands;
using ClaimManager.Application.Claims.Validators;

namespace ClaimManager.Application.UnitTests.Claims;

public sealed class RouteClaimForApprovalCommandValidatorTests
{
    private readonly RouteClaimForApprovalCommandValidator _validator = new();

    [Fact]
    public void Validator_accepts_valid_rationale()
    {
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), "Payment exceeds standard threshold, requires supervisor review."));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_empty_rationale()
    {
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RouteClaimForApprovalCommand.Rationale));
    }

    [Fact]
    public void Validator_rejects_rationale_below_minimum_length()
    {
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), "Too short"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RouteClaimForApprovalCommand.Rationale));
    }

    [Fact]
    public void Validator_rejects_rationale_exceeding_maximum_length()
    {
        var rationale = new string('x', 501);
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), rationale));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RouteClaimForApprovalCommand.Rationale));
    }

    [Fact]
    public void Validator_accepts_rationale_at_exact_minimum_length()
    {
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), "1234567890"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_accepts_rationale_at_exact_maximum_length()
    {
        var rationale = new string('x', 500);
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), rationale));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_rationale_that_is_only_padding_after_trimming()
    {
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), "         x"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RouteClaimForApprovalCommand.Rationale));
    }
}
