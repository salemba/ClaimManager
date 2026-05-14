namespace ClaimManager.Application.ClaimantCommunication.Validators;

using ClaimManager.Application.ClaimantCommunication.Commands;
using FluentValidation;

public sealed class SendClaimNotificationCommandValidator : AbstractValidator<SendClaimNotificationCommand>
{
    private static readonly HashSet<string> _allowedTypes = ["operational", "claimant-safe"];
    private static readonly HashSet<string> _allowedChannels = ["email"];

    public SendClaimNotificationCommandValidator()
    {
        RuleFor(x => x.CommunicationType)
            .NotEmpty()
            .Must(t => _allowedTypes.Contains(t))
            .WithMessage("Communication type must be 'operational' or 'claimant-safe'.");

        RuleFor(x => x.Channel)
            .NotEmpty()
            .Must(c => _allowedChannels.Contains(c))
            .WithMessage("Channel must be 'email'.");

        RuleFor(x => x.Recipient)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Body)
            .NotEmpty()
            .MaximumLength(4000);
    }
}
