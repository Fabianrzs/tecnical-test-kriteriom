using MediatR;

namespace Kriteriom.SharedKernel.CQRS;

public interface ICommand<TResult> : IRequest<TResult> { }

public interface ICommand : IRequest { }
