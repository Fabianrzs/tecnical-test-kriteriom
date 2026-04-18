FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5003

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Packages.props Directory.Packages.props

COPY src/SharedKernel/Kriteriom.SharedKernel.Domain/Kriteriom.SharedKernel.Domain.csproj             src/SharedKernel/Kriteriom.SharedKernel.Domain/
COPY src/SharedKernel/Kriteriom.SharedKernel.Contracts/Kriteriom.SharedKernel.Contracts.csproj       src/SharedKernel/Kriteriom.SharedKernel.Contracts/
COPY src/SharedKernel/Kriteriom.SharedKernel.Application/Kriteriom.SharedKernel.Application.csproj   src/SharedKernel/Kriteriom.SharedKernel.Application/
COPY src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/Kriteriom.SharedKernel.Infrastructure.csproj src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/

COPY src/Services/Audit/Kriteriom.Audit.Domain/Kriteriom.Audit.Domain.csproj               src/Services/Audit/Kriteriom.Audit.Domain/
COPY src/Services/Audit/Kriteriom.Audit.Infrastructure/Kriteriom.Audit.Infrastructure.csproj src/Services/Audit/Kriteriom.Audit.Infrastructure/
COPY src/Services/Audit/Kriteriom.Audit.API/Kriteriom.Audit.API.csproj                     src/Services/Audit/Kriteriom.Audit.API/

RUN dotnet restore src/Services/Audit/Kriteriom.Audit.API/Kriteriom.Audit.API.csproj

COPY src/SharedKernel/Kriteriom.SharedKernel.Domain/        src/SharedKernel/Kriteriom.SharedKernel.Domain/
COPY src/SharedKernel/Kriteriom.SharedKernel.Contracts/      src/SharedKernel/Kriteriom.SharedKernel.Contracts/
COPY src/SharedKernel/Kriteriom.SharedKernel.Application/    src/SharedKernel/Kriteriom.SharedKernel.Application/
COPY src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/ src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/

COPY src/Services/Audit/Kriteriom.Audit.Domain/        src/Services/Audit/Kriteriom.Audit.Domain/
COPY src/Services/Audit/Kriteriom.Audit.Infrastructure/ src/Services/Audit/Kriteriom.Audit.Infrastructure/
COPY src/Services/Audit/Kriteriom.Audit.API/            src/Services/Audit/Kriteriom.Audit.API/

RUN dotnet publish src/Services/Audit/Kriteriom.Audit.API/Kriteriom.Audit.API.csproj \
    -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kriteriom.Audit.API.dll"]
