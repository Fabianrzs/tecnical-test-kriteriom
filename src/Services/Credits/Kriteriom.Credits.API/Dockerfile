FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Packages.props Directory.Packages.props

COPY src/SharedKernel/Kriteriom.SharedKernel.Domain/Kriteriom.SharedKernel.Domain.csproj             src/SharedKernel/Kriteriom.SharedKernel.Domain/
COPY src/SharedKernel/Kriteriom.SharedKernel.Contracts/Kriteriom.SharedKernel.Contracts.csproj       src/SharedKernel/Kriteriom.SharedKernel.Contracts/
COPY src/SharedKernel/Kriteriom.SharedKernel.Application/Kriteriom.SharedKernel.Application.csproj   src/SharedKernel/Kriteriom.SharedKernel.Application/
COPY src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/Kriteriom.SharedKernel.Infrastructure.csproj src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/

COPY src/Services/Credits/Kriteriom.Credits.Domain/Kriteriom.Credits.Domain.csproj                   src/Services/Credits/Kriteriom.Credits.Domain/
COPY src/Services/Credits/Kriteriom.Credits.Application/Kriteriom.Credits.Application.csproj         src/Services/Credits/Kriteriom.Credits.Application/
COPY src/Services/Credits/Kriteriom.Credits.Infrastructure/Kriteriom.Credits.Infrastructure.csproj   src/Services/Credits/Kriteriom.Credits.Infrastructure/
COPY src/Services/Credits/Kriteriom.Credits.API/Kriteriom.Credits.API.csproj                         src/Services/Credits/Kriteriom.Credits.API/

RUN dotnet restore src/Services/Credits/Kriteriom.Credits.API/Kriteriom.Credits.API.csproj

COPY src/SharedKernel/Kriteriom.SharedKernel.Domain/        src/SharedKernel/Kriteriom.SharedKernel.Domain/
COPY src/SharedKernel/Kriteriom.SharedKernel.Contracts/      src/SharedKernel/Kriteriom.SharedKernel.Contracts/
COPY src/SharedKernel/Kriteriom.SharedKernel.Application/    src/SharedKernel/Kriteriom.SharedKernel.Application/
COPY src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/ src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/

COPY src/Services/Credits/Kriteriom.Credits.Domain/          src/Services/Credits/Kriteriom.Credits.Domain/
COPY src/Services/Credits/Kriteriom.Credits.Application/     src/Services/Credits/Kriteriom.Credits.Application/
COPY src/Services/Credits/Kriteriom.Credits.Infrastructure/  src/Services/Credits/Kriteriom.Credits.Infrastructure/
COPY src/Services/Credits/Kriteriom.Credits.API/             src/Services/Credits/Kriteriom.Credits.API/

RUN dotnet publish src/Services/Credits/Kriteriom.Credits.API/Kriteriom.Credits.API.csproj \
    -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:5001/health || exit 1
USER app
ENTRYPOINT ["dotnet", "Kriteriom.Credits.API.dll"]
