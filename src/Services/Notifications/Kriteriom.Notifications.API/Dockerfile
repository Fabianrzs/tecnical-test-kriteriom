FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/SharedKernel/Kriteriom.SharedKernel/Kriteriom.SharedKernel.csproj src/SharedKernel/Kriteriom.SharedKernel/
COPY src/Services/Notifications/Kriteriom.Notifications.Domain/Kriteriom.Notifications.Domain.csproj src/Services/Notifications/Kriteriom.Notifications.Domain/
COPY src/Services/Notifications/Kriteriom.Notifications.Infrastructure/Kriteriom.Notifications.Infrastructure.csproj src/Services/Notifications/Kriteriom.Notifications.Infrastructure/
COPY src/Services/Notifications/Kriteriom.Notifications.API/Kriteriom.Notifications.API.csproj src/Services/Notifications/Kriteriom.Notifications.API/

RUN dotnet restore src/Services/Notifications/Kriteriom.Notifications.API/Kriteriom.Notifications.API.csproj

COPY src/SharedKernel/Kriteriom.SharedKernel/ src/SharedKernel/Kriteriom.SharedKernel/
COPY src/Services/Notifications/Kriteriom.Notifications.Domain/ src/Services/Notifications/Kriteriom.Notifications.Domain/
COPY src/Services/Notifications/Kriteriom.Notifications.Infrastructure/ src/Services/Notifications/Kriteriom.Notifications.Infrastructure/
COPY src/Services/Notifications/Kriteriom.Notifications.API/ src/Services/Notifications/Kriteriom.Notifications.API/

RUN dotnet publish src/Services/Notifications/Kriteriom.Notifications.API/Kriteriom.Notifications.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 5004
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kriteriom.Notifications.API.dll"]
