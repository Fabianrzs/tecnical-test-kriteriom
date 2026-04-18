namespace Kriteriom.Credits.Domain.Exceptions;

public class InvalidCreditOperationException : Exception
{
    public InvalidCreditOperationException(string message)
        : base(message)
    {
    }

    public InvalidCreditOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
