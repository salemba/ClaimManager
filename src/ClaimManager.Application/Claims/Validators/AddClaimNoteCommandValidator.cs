namespace ClaimManager.Application.Claims.Validators;

using ClaimManager.Application.Claims.Commands;
using FluentValidation;

public sealed class AddClaimNoteCommandValidator : AbstractValidator<AddClaimNoteCommand>
{
    public AddClaimNoteCommandValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
    }
}