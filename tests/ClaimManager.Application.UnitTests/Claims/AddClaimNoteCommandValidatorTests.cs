using ClaimManager.Application.Claims.Commands;
using ClaimManager.Application.Claims.Validators;

namespace ClaimManager.Application.UnitTests.Claims;

public sealed class AddClaimNoteCommandValidatorTests
{
    private readonly AddClaimNoteCommandValidator _validator = new();

    [Fact]
    public void Empty_note_content_is_rejected()
    {
        var result = _validator.Validate(new AddClaimNoteCommand(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddClaimNoteCommand.Content));
    }

    [Fact]
    public void Non_empty_note_content_is_accepted()
    {
        var result = _validator.Validate(new AddClaimNoteCommand("Insured confirmed remediation start date."));

        Assert.True(result.IsValid);
    }
}