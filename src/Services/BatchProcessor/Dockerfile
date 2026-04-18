# Multi-stage build for BatchProcessor
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the projects we need
COPY src/SharedKernel/Kriteriom.SharedKernel/Kriteriom.SharedKernel.csproj \
     src/SharedKernel/Kriteriom.SharedKernel/

COPY src/Services/BatchProcessor/Kriteriom.BatchProcessor.csproj \
     src/Services/BatchProcessor/

RUN dotnet restore src/Services/BatchProcessor/Kriteriom.BatchProcessor.csproj

# Copy source files
COPY src/SharedKernel/Kriteriom.SharedKernel/ src/SharedKernel/Kriteriom.SharedKernel/
COPY src/Services/BatchProcessor/ src/Services/BatchProcessor/

RUN dotnet publish src/Services/BatchProcessor/Kriteriom.BatchProcessor.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

EXPOSE 5005

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Kriteriom.BatchProcessor.dll"]
