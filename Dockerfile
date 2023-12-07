FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Install necessary tools
RUN apt-get update -y \
    && apt-get install -y wget gnupg2 software-properties-common curl unzip

# Add Google Chrome to the repositories
RUN wget -q -O - https://dl-ssl.google.com/linux/linux_signing_key.pub | gpg --dearmor > /usr/share/keyrings/google-linux-keyring.gpg \
    && echo "deb [signed-by=/usr/share/keyrings/google-linux-keyring.gpg arch=amd64] http://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google.list

# Install Google Chrome
RUN apt-get update -y \
    && apt-get install -y google-chrome-stable \
    && rm -rf /var/lib/apt/lists/*

# Install ChromeDriver
RUN CHROMEDRIVER_VERSION=$(curl -sS chromedriver.storage.googleapis.com/LATEST_RELEASE) \
    && wget -N http://chromedriver.storage.googleapis.com/$CHROMEDRIVER_VERSION/chromedriver_linux64.zip -P ~/ \
    && unzip ~/chromedriver_linux64.zip -d ~/ \
    && rm ~/chromedriver_linux64.zip \
    && mv -f ~/chromedriver /usr/local/bin/chromedriver \
    && chown root:root /usr/local/bin/chromedriver \
    && chmod 0755 /usr/local/bin/chromedriver

# Continue with your existing build steps
WORKDIR /src
COPY ["src/PowerOutageNotifierService/PowerOutageNotifier.csproj", "."]
RUN dotnet restore "./PowerOutageNotifier.csproj"
COPY ["src/PowerOutageNotifierService/", "."]
WORKDIR "/src/."
RUN dotnet build "PowerOutageNotifier.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PowerOutageNotifier.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PowerOutageNotifier.dll"]
