namespace Kriteriom.SharedKernel.Domain;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
