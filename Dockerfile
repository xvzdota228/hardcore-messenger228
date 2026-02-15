# Используем официальный образ .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Устанавливаем рабочую директорию
WORKDIR /src

# Копируем файл проекта сервера
COPY Server/HardcoreServer.csproj Server/

# Восстанавливаем зависимости
RUN dotnet restore Server/HardcoreServer.csproj

# Копируем весь код (включая Shared)
COPY . .

# Собираем приложение
WORKDIR /src/Server
RUN dotnet build HardcoreServer.csproj -c Release -o /app/build
RUN dotnet publish HardcoreServer.csproj -c Release -o /app/publish

# Финальный образ
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Указываем порт (Railway передаст через переменную окружения PORT)
ENV DOTNET_ENVIRONMENT=Production

# Запускаем сервер
ENTRYPOINT ["dotnet", "HardcoreServer.dll"]
