# Используем официальный образ .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Копируем файлы проектов
COPY Shared/Shared.csproj Shared/
COPY Server/HardcoreServer.csproj Server/

# Восстанавливаем зависимости
RUN dotnet restore Server/HardcoreServer.csproj

# Копируем весь исходный код
COPY Shared/ Shared/
COPY Server/ Server/

# Собираем и публикуем
WORKDIR /src/Server
RUN dotnet publish -c Release -o /app/publish

# Финальный образ
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "HardcoreServer.dll"]
