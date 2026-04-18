FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/SharedKernel/Kriteriom.SharedKernel/Kriteriom.SharedKernel.csproj src/SharedKernel/Kriteriom.SharedKernel/
COPY src/Services/Audit/Kriteriom.Audit.Domain/Kriteriom.Audit.Domain.csproj src/Services/Audit/Kriteriom.Audit.Domain/
COPY src/Services/Audit/Kriteriom.Audit.Infrastructure/Kriteriom.Audit.Infrastructure.csproj src/Services/Audit/Kriteriom.Audit.Infrastructure/
COPY src/Services/Audit/Kriteriom.Audit.API/Kriteriom.Audit.API.csproj src/Services/Audit/Kriteriom.Audit.API/

RUN dotnet restore src/Services/Audit/Kriteriom.Audit.API/Kriteriom.Audit.API.csproj

COPY src/SharedKernel/Kriteriom.SharedKernel/ src/SharedKernel/Kriteriom.SharedKernel/
COPY src/Services/Audit/Kriteriom.Audit.Domain/ src/Services/Audit/Kriteriom.Audit.Domain/
COPY src/Services/Audit/Kriteriom.Audit.Infrastructure/ src/Services/Audit/Kriteriom.Audit.Infrastructure/
COPY src/Services/Audit/Kriteriom.Audit.API/ src/Services/Audit/Kriteriom.Audit.API/

RUN dotnet publish src/Services/Audit/Kriteriom.Audit.API/Kriteriom.Audit.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 5003
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kriteriom.Audit.API.dll"]
