FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# DartSassBuilder 1.1.0 targets net8.0 even though the application targets net10.0.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && curl -fsSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin \
    --channel 8.0 \
    --runtime dotnet \
    --install-dir /usr/share/dotnet \
    --no-path \
    && rm -rf /var/lib/apt/lists/*

COPY . .
RUN dotnet restore ScoreTracker/ScoreTracker/ScoreTracker.Web.csproj
RUN dotnet publish ScoreTracker/ScoreTracker/ScoreTracker.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ScoreTracker.Web.dll"]
