namespace Kriteriom.Credits.Domain.Exceptions;

public class DebtCapacityExceededException(decimal projectedDti)
    : InvalidCreditOperationException(
        $"La suma de pagos mensuales proyectada ({projectedDti:F1}%) supera el límite del 60% de los ingresos.");
