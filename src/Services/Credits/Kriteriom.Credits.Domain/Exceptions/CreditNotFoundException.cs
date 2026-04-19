namespace Kriteriom.Credits.Domain.Exceptions;

public class CreditNotFoundException(Guid id) : Exception($"Credit {id} not found");
