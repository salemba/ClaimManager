namespace ClaimManager.Application.Claims.Validators;

using ClaimManager.Application.Claims.Commands;
using FluentValidation;

public sealed class UploadClaimDocumentCommandValidator : AbstractValidator<UploadClaimDocumentCommand>
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions =
    [
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png"
    ];

    public UploadClaimDocumentCommandValidator()
    {
        RuleFor(x => x).Custom((command, context) =>
        {
            if (string.IsNullOrWhiteSpace(command.FileName) || command.Content.Length == 0)
            {
                context.AddFailure("File", "A document is required.");
                return;
            }

            var extension = Path.GetExtension(command.FileName).Trim().ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                context.AddFailure("File", "Supported file types are PDF, JPG, JPEG, and PNG.");
            }

            if (command.FileSizeBytes <= 0)
            {
                context.AddFailure("File", "A document is required.");
            }
            else if (command.FileSizeBytes > MaxFileSizeBytes)
            {
                context.AddFailure("File", "File size must be 10 MB or smaller.");
            }
        });
    }
}