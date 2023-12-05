FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PowerOutageNotifier.csproj", "."]
RUN dotnet restore "./PowerOutageNotifier.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "PowerOutageNotifier.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PowerOutageNotifier.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY ["bot-token.txt", "/app"]
ENTRYPOINT ["dotnet", "PowerOutageNotifier.dll"]
