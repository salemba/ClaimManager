using ClaimManager.Application.Claims.Commands;
using ClaimManager.Application.Claims.Validators;

namespace ClaimManager.Application.UnitTests.Claims;

public sealed class CreateAndUpdateClaimCommandValidatorTests
{
    [Fact]
    public void Create_validator_rejects_missing_required_fields()
    {
        var validator = new CreateClaimCommandValidator();

        var result = validator.Validate(new CreateClaimCommand(string.Empty, string.Empty, string.Empty, string.Empty, DateTime.UtcNow.AddDays(1), string.Empty, string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateClaimCommand.ClaimantName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateClaimCommand.ClaimantEmail));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateClaimCommand.LossDateUtc));
    }

    [Fact]
    public void Update_validator_accepts_complete_payload()
    {
        var validator = new UpdateClaimCommandValidator();

        var result = validator.Validate(new UpdateClaimCommand(
            Guid.NewGuid(),
            "Jordan Avery",
            "jordan.avery@example.com",
            "555-0100",
            "POL-0100",
            DateTime.UtcNow.AddDays(-1),
            "Water damage",
            "Pipe burst in lower level."));

        Assert.True(result.IsValid);
    }
}