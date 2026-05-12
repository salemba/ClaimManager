namespace ClaimManager.Application.Claims.Validators;

using ClaimManager.Application.Claims.Commands;
using FluentValidation;

public sealed class UpdateClaimCommandValidator : AbstractValidator<UpdateClaimCommand>
{
    public UpdateClaimCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ClaimantName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.ClaimantEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.ClaimantPhone).NotEmpty().MaximumLength(32);
        RuleFor(x => x.PolicyNumber).NotEmpty().MaximumLength(64);
        RuleFor(x => x.LossDateUtc)
            .NotEmpty()
            .Must(lossDateUtc => lossDateUtc <= DateTime.UtcNow)
            .WithMessage("Loss date must be in the past.");
        RuleFor(x => x.LossType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.LossDescription).NotEmpty().MaximumLength(2000);
    }
}