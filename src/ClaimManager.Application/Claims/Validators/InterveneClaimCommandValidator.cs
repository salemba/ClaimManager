namespace ClaimManager.Application.Claims.Validators;

using ClaimManager.Application.Claims.Commands;
using FluentValidation;

public sealed class InterveneClaimCommandValidator : AbstractValidator<InterveneClaimCommand>
{
    public InterveneClaimCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.NewAdjusterId).MaximumLength(100);
        RuleFor(x => x.NewState).MaximumLength(64);
        RuleFor(x => x.RowVersion).NotEmpty();

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.NewAdjusterId) || !string.IsNullOrWhiteSpace(x.NewState))
            .WithMessage("At least one change (Adjuster ID or State) must be provided for intervention.");
    }
}
