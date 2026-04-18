FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5004

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Packages.props Directory.Packages.props

COPY src/SharedKernel/Kriteriom.SharedKernel.Domain/Kriteriom.SharedKernel.Domain.csproj             src/SharedKernel/Kriteriom.SharedKernel.Domain/
COPY src/SharedKernel/Kriteriom.SharedKernel.Contracts/Kriteriom.SharedKernel.Contracts.csproj       src/SharedKernel/Kriteriom.SharedKernel.Contracts/
COPY src/SharedKernel/Kriteriom.SharedKernel.Application/Kriteriom.SharedKernel.Application.csproj   src/SharedKernel/Kriteriom.SharedKernel.Application/
COPY src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/Kriteriom.SharedKernel.Infrastructure.csproj src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/

COPY src/Services/Notifications/Kriteriom.Notifications.Domain/Kriteriom.Notifications.Domain.csproj               src/Services/Notifications/Kriteriom.Notifications.Domain/
COPY src/Services/Notifications/Kriteriom.Notifications.Infrastructure/Kriteriom.Notifications.Infrastructure.csproj src/Services/Notifications/Kriteriom.Notifications.Infrastructure/
COPY src/Services/Notifications/Kriteriom.Notifications.API/Kriteriom.Notifications.API.csproj                     src/Services/Notifications/Kriteriom.Notifications.API/

RUN dotnet restore src/Services/Notifications/Kriteriom.Notifications.API/Kriteriom.Notifications.API.csproj

COPY src/SharedKernel/Kriteriom.SharedKernel.Domain/        src/SharedKernel/Kriteriom.SharedKernel.Domain/
COPY src/SharedKernel/Kriteriom.SharedKernel.Contracts/      src/SharedKernel/Kriteriom.SharedKernel.Contracts/
COPY src/SharedKernel/Kriteriom.SharedKernel.Application/    src/SharedKernel/Kriteriom.SharedKernel.Application/
COPY src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/ src/SharedKernel/Kriteriom.SharedKernel.Infrastructure/

COPY src/Services/Notifications/Kriteriom.Notifications.Domain/        src/Services/Notifications/Kriteriom.Notifications.Domain/
COPY src/Services/Notifications/Kriteriom.Notifications.Infrastructure/ src/Services/Notifications/Kriteriom.Notifications.Infrastructure/
COPY src/Services/Notifications/Kriteriom.Notifications.API/            src/Services/Notifications/Kriteriom.Notifications.API/

RUN dotnet publish src/Services/Notifications/Kriteriom.Notifications.API/Kriteriom.Notifications.API.csproj \
    -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kriteriom.Notifications.API.dll"]
