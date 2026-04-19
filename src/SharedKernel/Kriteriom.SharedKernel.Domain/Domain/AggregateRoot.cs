namespace Kriteriom.SharedKernel.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public int Version { get; protected set; }

    protected AggregateRoot() : base() { }

    protected AggregateRoot(Guid id) : base(id) { }

    public IReadOnlyList<IDomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
        Version++;
    }

    protected void IncrementVersion() => Version++;

    public void ClearDomainEvents() => _domainEvents.Clear();
}
