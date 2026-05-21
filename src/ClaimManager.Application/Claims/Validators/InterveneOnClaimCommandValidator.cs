using FluentValidation;

namespace ClaimManager.Application.Claims.Validators;

using ClaimManager.Application.Claims.Commands;

public sealed class InterveneOnClaimCommandValidator : AbstractValidator<InterveneOnClaimCommand>
{
    public InterveneOnClaimCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NewOwnerId).NotEmpty().WithMessage("New owner is required.");
        RuleFor(x => x.TargetStatus).NotEmpty().WithMessage("Target status is required.");
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
