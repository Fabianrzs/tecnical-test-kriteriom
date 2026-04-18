# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for restore
COPY src/SharedKernel/Kriteriom.SharedKernel/Kriteriom.SharedKernel.csproj src/SharedKernel/Kriteriom.SharedKernel/
COPY src/Services/Risk/Kriteriom.Risk.Domain/Kriteriom.Risk.Domain.csproj src/Services/Risk/Kriteriom.Risk.Domain/
COPY src/Services/Risk/Kriteriom.Risk.API/Kriteriom.Risk.API.csproj src/Services/Risk/Kriteriom.Risk.API/

# Restore
RUN dotnet restore src/Services/Risk/Kriteriom.Risk.API/Kriteriom.Risk.API.csproj

# Copy source
COPY src/SharedKernel/Kriteriom.SharedKernel/ src/SharedKernel/Kriteriom.SharedKernel/
COPY src/Services/Risk/Kriteriom.Risk.Domain/ src/Services/Risk/Kriteriom.Risk.Domain/
COPY src/Services/Risk/Kriteriom.Risk.API/ src/Services/Risk/Kriteriom.Risk.API/

# Build and publish
RUN dotnet publish src/Services/Risk/Kriteriom.Risk.API/Kriteriom.Risk.API.csproj \
    -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 5002

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Kriteriom.Risk.API.dll"]
