using ClaimManager.Application.Claims.Commands;
using ClaimManager.Application.Claims.Validators;

namespace ClaimManager.Application.UnitTests.Claims;

public sealed class RouteClaimForApprovalCommandValidatorTests
{
    private readonly RouteClaimForApprovalCommandValidator _validator = new();
    private static readonly byte[] RowVersion = [];

    [Fact]
    public void Validator_accepts_valid_rationale()
    {
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), "Payment exceeds standard threshold, requires supervisor review.", RowVersion));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_empty_rationale()
    {
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), string.Empty, RowVersion));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RouteClaimForApprovalCommand.Rationale));
    }

    [Fact]
    public void Validator_rejects_rationale_below_minimum_length()
    {
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), "Too short", RowVersion));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RouteClaimForApprovalCommand.Rationale));
    }

    [Fact]
    public void Validator_rejects_rationale_exceeding_maximum_length()
    {
        var rationale = new string('x', 501);
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), rationale, RowVersion));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RouteClaimForApprovalCommand.Rationale));
    }

    [Fact]
    public void Validator_accepts_rationale_at_exact_minimum_length()
    {
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), "1234567890", RowVersion));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_accepts_rationale_at_exact_maximum_length()
    {
        var rationale = new string('x', 500);
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), rationale, RowVersion));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_rationale_that_is_only_padding_after_trimming()
    {
        var result = _validator.Validate(new RouteClaimForApprovalCommand(Guid.NewGuid(), "         x", RowVersion));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RouteClaimForApprovalCommand.Rationale));
    }
}
