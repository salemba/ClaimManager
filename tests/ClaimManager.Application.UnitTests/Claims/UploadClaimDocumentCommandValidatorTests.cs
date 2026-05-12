using ClaimManager.Application.Claims.Commands;
using ClaimManager.Application.Claims.Validators;

namespace ClaimManager.Application.UnitTests.Claims;

public sealed class UploadClaimDocumentCommandValidatorTests
{
    private readonly UploadClaimDocumentCommandValidator _validator = new();

    [Fact]
    public void Unsupported_extension_is_rejected_on_file_field()
    {
        var result = _validator.Validate(new UploadClaimDocumentCommand(
            "payload.exe",
            "application/octet-stream",
            128,
            [1, 2, 3]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "File");
    }

    [Fact]
    public void Oversized_file_is_rejected_on_file_field()
    {
        var result = _validator.Validate(new UploadClaimDocumentCommand(
            "estimate.pdf",
            "application/pdf",
            UploadClaimDocumentCommandValidator.MaxFileSizeBytes + 1,
            [1]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "File" && error.ErrorMessage.Contains("10 MB", StringComparison.Ordinal));
    }

    [Fact]
    public void Supported_file_with_content_is_accepted()
    {
        var result = _validator.Validate(new UploadClaimDocumentCommand(
            "estimate.pdf",
            "application/pdf",
            128,
            [1, 2, 3]));

        Assert.True(result.IsValid);
    }
}