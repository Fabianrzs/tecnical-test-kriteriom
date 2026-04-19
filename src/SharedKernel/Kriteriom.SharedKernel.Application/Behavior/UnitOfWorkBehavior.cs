using Kriteriom.SharedKernel.Application.Services;
using Kriteriom.SharedKernel.CQRS;
using MediatR;

namespace Kriteriom.SharedKernel.CQRS;

public class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICommand<TResponse>)
            return await next();

        TResponse response = default!;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            response = await next();
        }, cancellationToken);
        return response;
    }
}
