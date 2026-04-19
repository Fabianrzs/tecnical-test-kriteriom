using Kriteriom.SharedKernel.Application.Services;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;
using MediatR;

namespace Kriteriom.Tests.Application;

public class UnitOfWorkBehaviorTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    // Satisfies ICommand<Result<string>> → behavior wraps in transaction
    private record RegularCommand : ICommand<Result<string>>;

    // Opts out of the behavior-managed transaction
    private record OwnTransactionCommand : ICommand<Result<string>>;

    // Plain IRequest (query) — does NOT implement ICommand<> → behavior skips it
    private record QueryRequest : IRequest<Result<string>>;

    private static RequestHandlerDelegate<TResponse> SuccessDelegate<TResponse>(TResponse value)
        => () => Task.FromResult(value);

    [Fact]
    public async Task Handle_RegularCommand_WrapsInTransaction()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<Task>>(0)());

        var behavior = new UnitOfWorkBehavior<RegularCommand, Result<string>>(_unitOfWork);
        var result = await behavior.Handle(
            new RegularCommand(),
            SuccessDelegate(Result<string>.Success("ok")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OwnTransactionCommand_SkipsUnitOfWork()
    {
        var behavior = new UnitOfWorkBehavior<OwnTransactionCommand, Result<string>>(_unitOfWork);
        var result = await behavior.Handle(
            new OwnTransactionCommand(),
            SuccessDelegate(Result<string>.Success("ok")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QueryRequest_SkipsUnitOfWork()
    {
        var behavior = new UnitOfWorkBehavior<QueryRequest, Result<string>>(_unitOfWork);
        var result = await behavior.Handle(
            new QueryRequest(),
            SuccessDelegate(Result<string>.Success("query-result")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RegularCommand_PropagatesHandlerFailure()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<Task>>(0)());

        var behavior = new UnitOfWorkBehavior<RegularCommand, Result<string>>(_unitOfWork);
        var result = await behavior.Handle(
            new RegularCommand(),
            SuccessDelegate(Result<string>.Failure("something broke", "ERR")),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ERR");
    }
}
