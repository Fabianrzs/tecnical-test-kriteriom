FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/Credits/Kriteriom.Credits.API/Kriteriom.Credits.API.csproj", "src/Services/Credits/Kriteriom.Credits.API/"]
COPY ["src/Services/Credits/Kriteriom.Credits.Application/Kriteriom.Credits.Application.csproj", "src/Services/Credits/Kriteriom.Credits.Application/"]
COPY ["src/Services/Credits/Kriteriom.Credits.Domain/Kriteriom.Credits.Domain.csproj", "src/Services/Credits/Kriteriom.Credits.Domain/"]
COPY ["src/Services/Credits/Kriteriom.Credits.Infrastructure/Kriteriom.Credits.Infrastructure.csproj", "src/Services/Credits/Kriteriom.Credits.Infrastructure/"]
COPY ["src/SharedKernel/Kriteriom.SharedKernel/Kriteriom.SharedKernel.csproj", "src/SharedKernel/Kriteriom.SharedKernel/"]
RUN dotnet restore "src/Services/Credits/Kriteriom.Credits.API/Kriteriom.Credits.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Credits/Kriteriom.Credits.API"
RUN dotnet build "Kriteriom.Credits.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Kriteriom.Credits.API.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kriteriom.Credits.API.dll"]
