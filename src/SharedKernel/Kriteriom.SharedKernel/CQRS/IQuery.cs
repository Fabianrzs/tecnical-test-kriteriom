using MediatR;

namespace Kriteriom.SharedKernel.CQRS;

public interface IQuery<TResult> : IRequest<TResult> { }
