using Kriteriom.Credits.Application.Commands.CreateCredit;
using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Services;
using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kriteriom.Tests.Application;

public class CreateCreditCommandHandlerTests
{
    private readonly ICreditRepository _creditRepo = Substitute.For<ICreditRepository>();
    private readonly IClientRepository _clientRepo = Substitute.For<IClientRepository>();
    private readonly IOutboxRepository _outboxRepo = Substitute.For<IOutboxRepository>();
    private readonly IIdempotencyService _idempotency = Substitute.For<IIdempotencyService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly CreateCreditCommandHandler _handler;

    public CreateCreditCommandHandlerTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<Task>>(0)());

        _handler = new CreateCreditCommandHandler(
            _creditRepo, _clientRepo, _outboxRepo,
            _idempotency, _unitOfWork,
            NullLogger<CreateCreditCommandHandler>.Instance);
    }

    private static CreateCreditCommand ValidCommand(Guid? clientId = null) => new()
    {
        ClientId       = clientId ?? Guid.NewGuid(),
        Amount         = 5_000_000m,
        InterestRate   = 0.18m,
        TermMonths     = 36,
        IdempotencyKey = Guid.NewGuid().ToString()
    };

    private static Client BuildClient(decimal income = 10_000_000m)
        => Client.Create("Test User", "test@example.com", "123456789", income, EmploymentStatus.Employed);

    [Fact]
    public async Task Handle_ClientNotFound_ReturnsFailureWithCode()
    {
        var cmd = ValidCommand();
        _idempotency.GetAsync(cmd.IdempotencyKey).Returns((string?)null);
        _clientRepo.GetByIdAsync(cmd.ClientId).Returns((Client?)null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CLIENT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ProjectedDtiExceeds60Pct_ReturnsDebtCapacityExceeded()
    {
        var client = BuildClient(income: 200_000m);
        var cmd = new CreateCreditCommand
        {
            ClientId       = client.Id,
            Amount         = 5_000_000m,
            InterestRate   = 0.18m,
            TermMonths     = 36,
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        _idempotency.GetAsync(cmd.IdempotencyKey).Returns((string?)null);
        _clientRepo.GetByIdAsync(cmd.ClientId).Returns(client);
        _creditRepo.GetActiveCreditsForClientAsync(cmd.ClientId).Returns([]);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("DEBT_CAPACITY_EXCEEDED");
    }

    [Fact]
    public async Task Handle_IdempotencyKeyAlreadyUsed_ReturnsCachedResult()
    {
        var cmd = ValidCommand();
        var cached = new CreditDto
        {
            Id = Guid.NewGuid(), ClientId = cmd.ClientId, Amount = cmd.Amount,
            InterestRate = cmd.InterestRate, TermMonths = cmd.TermMonths,
            Status = CreditStatus.Pending, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var json = System.Text.Json.JsonSerializer.Serialize(cached,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        _idempotency.GetAsync(cmd.IdempotencyKey).Returns(json);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(cached.Id);
        await _clientRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Handle_ValidRequest_SavesCreditAndOutboxMessage()
    {
        var client = BuildClient();
        var cmd = new CreateCreditCommand
        {
            ClientId       = client.Id,
            Amount         = 5_000_000m,
            InterestRate   = 0.18m,
            TermMonths     = 36,
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        _idempotency.GetAsync(cmd.IdempotencyKey).Returns((string?)null);
        _clientRepo.GetByIdAsync(cmd.ClientId).Returns(client);
        _creditRepo.GetActiveCreditsForClientAsync(cmd.ClientId).Returns([]);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Amount.Should().Be(cmd.Amount);
        result.Value.Status.Should().Be(CreditStatus.Pending);

        await _creditRepo.Received(1).AddAsync(Arg.Any<Credit>(), Arg.Any<CancellationToken>());
        await _outboxRepo.Received(1).AddAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        await _idempotency.Received(1).SetAsync(cmd.IdempotencyKey, Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_IncludesExistingDebtInProjectedDti()
    {
        var client = BuildClient(income: 1_000_000m);
        var existingCredit = Credit.Create(client.Id, 1_200_000m, 0m, 12);

        var cmd = new CreateCreditCommand
        {
            ClientId       = client.Id,
            Amount         = 5_000_000m,
            InterestRate   = 0.18m,
            TermMonths     = 36,
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        _idempotency.GetAsync(cmd.IdempotencyKey).Returns((string?)null);
        _clientRepo.GetByIdAsync(cmd.ClientId).Returns(client);
        _creditRepo.GetActiveCreditsForClientAsync(cmd.ClientId).Returns([existingCredit]);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
