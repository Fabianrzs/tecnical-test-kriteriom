using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Commands.RecalculateCreditStatuses;

public record RecalculateCreditStatusesCommand(int BatchSize = 500) : ICommand<Result<RecalculateCreditStatusesSummary>>;

public record RecalculateCreditStatusesSummary(int Processed, int Updated, int Errors);
