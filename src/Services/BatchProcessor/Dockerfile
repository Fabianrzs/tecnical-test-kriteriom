FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5005
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Packages.props Directory.Packages.props

COPY src/SharedKernel/Kriteriom.SharedKernel.Domain/Kriteriom.SharedKernel.Domain.csproj             src/SharedKernel/Kriteriom.SharedKernel.Domain/
COPY src/SharedKernel/Kriteriom.SharedKernel.Contracts/Kriteriom.SharedKernel.Contracts.csproj       src/SharedKernel/Kriteriom.SharedKernel.Contracts/
COPY src/SharedKernel/Kriteriom.SharedKernel.Application/Kriteriom.SharedKernel.Application.csproj   src/SharedKernel/Kriteriom.SharedKernel.Application/
COPY src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/Kriteriom.SharedKernel.Infrastructure.csproj src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/

COPY src/Services/BatchProcessor/Kriteriom.BatchProcessor.csproj src/Services/BatchProcessor/

RUN dotnet restore src/Services/BatchProcessor/Kriteriom.BatchProcessor.csproj

COPY src/SharedKernel/Kriteriom.SharedKernel.Domain/        src/SharedKernel/Kriteriom.SharedKernel.Domain/
COPY src/SharedKernel/Kriteriom.SharedKernel.Contracts/      src/SharedKernel/Kriteriom.SharedKernel.Contracts/
COPY src/SharedKernel/Kriteriom.SharedKernel.Application/    src/SharedKernel/Kriteriom.SharedKernel.Application/
COPY src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/ src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/

COPY src/Services/BatchProcessor/ src/Services/BatchProcessor/

RUN dotnet publish src/Services/BatchProcessor/Kriteriom.BatchProcessor.csproj \
    -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:5005/health || exit 1
USER app
ENTRYPOINT ["dotnet", "Kriteriom.BatchProcessor.dll"]
