FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Gateway/Kriteriom.Gateway/Kriteriom.Gateway.csproj", "src/Gateway/Kriteriom.Gateway/"]
COPY ["src/SharedKernel/Kriteriom.SharedKernel/Kriteriom.SharedKernel.csproj", "src/SharedKernel/Kriteriom.SharedKernel/"]
RUN dotnet restore "src/Gateway/Kriteriom.Gateway/Kriteriom.Gateway.csproj"
COPY . .
WORKDIR "/src/src/Gateway/Kriteriom.Gateway"
RUN dotnet build "Kriteriom.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Kriteriom.Gateway.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kriteriom.Gateway.dll"]
