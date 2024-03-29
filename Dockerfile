FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PowerOutageNotifierService/PowerOutageNotifier.csproj", "."]
RUN dotnet restore "./PowerOutageNotifier.csproj"
COPY ["src/PowerOutageNotifierService/", "."]
WORKDIR "/src/."
RUN dotnet build "PowerOutageNotifier.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PowerOutageNotifier.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PowerOutageNotifier.dll"]
