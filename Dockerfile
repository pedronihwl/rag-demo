# Base stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

RUN apt-get update && apt-get install -y \
    curl \
    gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs

WORKDIR /src
COPY . .

RUN dotnet restore "Backend/Backend.csproj" --no-cache
RUN dotnet publish "Backend/Backend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Environment variables
ENV AZURE_KEY_VAULT_ENDPOINT="https://kv-simonaggio.vault.azure.net/"
ENV OPENAI_EMBEDDING_DEPLOYMENT=text-embedding-ada-002
ENV OPENAI_CHAT_DEPLOYMENT=gpt-4o-mini


ENTRYPOINT ["dotnet", "Backend.dll"]

# docker build -t crsimonaggio.azurecr.io/ragappbackend:demo .
# docker push crsimonaggio.azurecr.io/ragappbackend:demo

