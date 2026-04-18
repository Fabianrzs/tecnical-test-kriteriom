namespace Kriteriom.Credits.Domain.Exceptions;

public class CreditNotFoundException : Exception
{
    public CreditNotFoundException(Guid id)
        : base($"Credit {id} not found")
    {
    }
}
