# Используем официальный образ .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Устанавливаем рабочую директорию
WORKDIR /app

# Копируем файлы проекта
COPY Server/HardcoreServer.csproj ./Server/
COPY Shared/Models.cs ./Shared/

# Восстанавливаем зависимости
WORKDIR /app/Server
RUN dotnet restore

# Копируем весь код
WORKDIR /app
COPY Server/ ./Server/
COPY Shared/ ./Shared/

# Собираем приложение
WORKDIR /app/Server
RUN dotnet publish -c Release -o /app/publish

# Финальный образ
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Указываем порт (Railway передаст через переменную окружения PORT)
EXPOSE 8080

# Запускаем сервер
ENTRYPOINT ["dotnet", "HardcoreServer.dll"]
