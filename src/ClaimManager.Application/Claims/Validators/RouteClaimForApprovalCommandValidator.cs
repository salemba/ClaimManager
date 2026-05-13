namespace ClaimManager.Application.Claims.Validators;

using ClaimManager.Application.Claims.Commands;
using FluentValidation;

public sealed class RouteClaimForApprovalCommandValidator : AbstractValidator<RouteClaimForApprovalCommand>
{
    public RouteClaimForApprovalCommandValidator()
    {
        RuleFor(x => x.Rationale)
            .NotEmpty().WithMessage("A rationale is required to route the claim for payment approval.")
            .Must(rationale => rationale.Trim().Length >= 10).WithMessage("Rationale must be at least 10 characters.")
            .Must(rationale => rationale.Trim().Length <= 500).WithMessage("Rationale cannot exceed 500 characters.");
    }
}
