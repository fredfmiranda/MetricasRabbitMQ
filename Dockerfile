# Use a imagem SDK como build
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# Copie o csproj e restaure qualquer dependência
COPY *.csproj ./
RUN dotnet restore

# Copie o projeto e publique
COPY . ./
RUN dotnet publish -c Release -o out

# Gere a imagem da aplicação
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "MetricasRabbitMQ.dll"]
