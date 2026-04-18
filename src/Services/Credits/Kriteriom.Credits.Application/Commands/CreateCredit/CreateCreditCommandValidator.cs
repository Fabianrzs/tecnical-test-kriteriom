using FluentValidation;

namespace Kriteriom.Credits.Application.Commands.CreateCredit;

public class CreateCreditCommandValidator : AbstractValidator<CreateCreditCommand>
{
    public CreateCreditCommandValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty()
            .WithMessage("ClientId cannot be empty");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero")
            .LessThanOrEqualTo(10_000_000)
            .WithMessage("Amount cannot exceed 10,000,000");

        RuleFor(x => x.InterestRate)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Interest rate cannot be negative")
            .LessThanOrEqualTo(1)
            .WithMessage("Interest rate cannot exceed 1 (100%)");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("IdempotencyKey is required");
    }
}
