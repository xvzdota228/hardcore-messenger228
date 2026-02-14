@echo off
chcp 65001 >nul
echo.
echo ╔══════════════════════════════════════════════════════════╗
echo ║   🚀 HardcoreMessenger HARD+ - Сборка EXE файла         ║
echo ╚══════════════════════════════════════════════════════════╝
echo.

cd Client

echo [ПРОВЕРКА] 🔍 Проверка иконки приложения...
if exist "hardcore.ico" (
    echo ✅ Иконка найдена: hardcore.ico
) else (
    echo ⚠️ Иконка не найдена!
    echo.
    echo Создайте файл hardcore.ico в папке Client\
    echo Или запустите CREATE_ICON.bat для помощи
    echo.
    echo Продолжить без иконки? (Enter = Да, Ctrl+C = Нет)
    pause
)

echo.
echo [1/4] 🧹 Очистка старых сборок...
if exist bin\Release rmdir /s /q bin\Release
if exist obj\Release rmdir /s /q obj\Release

echo [2/4] 🔨 Компиляция проекта HARD+ Edition...
echo.
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none

if %errorlevel% neq 0 (
    echo.
    echo ❌ ОШИБКА при сборке!
    echo Убедитесь что установлен .NET 8.0 SDK
    echo Скачать: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo.
echo [3/4] 📦 Создание дистрибутива...

set OUTPUT_DIR=..\Distribution
set PUBLISH_DIR=bin\Release\net8.0-windows\win-x64\publish

if not exist %OUTPUT_DIR% mkdir %OUTPUT_DIR%

:: Копируем EXE
copy "%PUBLISH_DIR%\HardcoreMessenger.exe" "%OUTPUT_DIR%\" >nul

:: Копируем иконку если есть
if exist "hardcore.ico" (
    copy "hardcore.ico" "%OUTPUT_DIR%\" >nul
)

:: Создаем ClientConfig.txt
echo ws://YOUR_RAILWAY_URL_HERE > "%OUTPUT_DIR%\ClientConfig.txt"

:: Создаем инструкцию для пользователя
(
echo ═══════════════════════════════════════════════════════
echo    🚀 HardcoreMessenger HARD+ Edition
echo ═══════════════════════════════════════════════════════
echo.
echo 📋 Что нужно сделать:
echo.
echo 1. Откройте файл ClientConfig.txt
echo 2. Замените YOUR_RAILWAY_URL_HERE на адрес вашего сервера
echo    Пример: ws://hardcore-messenger-production.up.railway.app
echo 3. Сохраните файл
echo 4. Запустите HardcoreMessenger.exe
echo.
echo ═══════════════════════════════════════════════════════
echo    ✨ HARD+ ПРЕМИУМ ВОЗМОЖНОСТИ:
echo ═══════════════════════════════════════════════════════
echo.
echo ✅ Групповые чаты
echo ✅ Голосовые сообщения
echo ✅ Пересылка сообщений
echo ✅ Поиск по истории
echo ✅ Темная/светлая тема
echo ✅ Закрепленные сообщения
echo ✅ Упоминания @username
echo ✅ 🌟 HARD+ Эмодзи после ника
echo ✅ 🌟 HARD+ Премиум значок
echo ✅ 🌟 HARD+ Эксклюзивные стикеры
echo ✅ 🌟 HARD+ Кастомные темы
echo ✅ 🌟 HARD+ Анимированные эмодзи
echo ✅ 🌟 HARD+ Опросы
echo ✅ 🌟 HARD+ Геолокация
echo.
echo 💡 Системные требования:
echo    - Windows 10/11
echo    - Интернет соединение
echo.
echo 🔒 Безопасность:
echo    - Все сообщения проходят через ваш сервер
echo    - Никакие данные не передаются третьим лицам
echo.
echo 🌟 Активация HARD+:
echo    - Бесплатно для всех пользователей!
echo    - Просто зайдите в Настройки → HARD+
echo.
echo ═══════════════════════════════════════════════════════
) > "%OUTPUT_DIR%\README.txt"

:: Создаем информацию о HARD+
(
echo ═══════════════════════════════════════════════════════
echo    🌟 HARD+ ПРЕМИУМ ФУНКЦИИ
echo ═══════════════════════════════════════════════════════
echo.
echo HARD+ - это расширенные возможности HardcoreMessenger,
echo доступные БЕСПЛАТНО для всех пользователей!
echo.
echo 📋 Что входит в HARD+:
echo.
echo 1. 💎 ЭМОДЗИ ПОСЛЕ НИКА
echo    Добавьте любой эмодзи после своего имени
echo    Пример: "Ваше_Имя 🔥" или "Username ⚡"
echo.
echo 2. 🎨 КАСТОМНЫЕ ТЕМЫ
echo    Выбирайте из эксклюзивных цветовых схем:
echo    • Неоновая
echo    • Космическая
echo    • Матрица
echo    • Океан
echo    • Закат
echo.
echo 3. 🎭 ЭКСКЛЮЗИВНЫЕ СТИКЕРЫ
echo    Доступ к премиум набору стикеров
echo.
echo 4. ⚡ АНИМИРОВАННЫЕ ЭМОДЗИ
echo    Используйте анимированные реакции
echo.
echo 5. 📊 СОЗДАНИЕ ОПРОСОВ
echo    Создавайте опросы в чатах и группах
echo.
echo 6. 📍 ОТПРАВКА ГЕОЛОКАЦИИ
echo    Делитесь своим местоположением
echo.
echo 7. 🔥 ПРЕМИУМ ЗНАЧОК
echo    Красивый значок рядом с именем
echo.
echo КАК АКТИВИРОВАТЬ:
echo.
echo 1. Запустите HardcoreMessenger
echo 2. Зайдите в Настройки
echo 3. Нажмите "Активировать HARD+"
echo 4. Настройте свой профиль!
echo.
echo ═══════════════════════════════════════════════════════
) > "%OUTPUT_DIR%\HARD_PLUS_INFO.txt"

cd ..

echo.
echo [4/4] ✅ Проверка результата...
if exist "%OUTPUT_DIR%\HardcoreMessenger.exe" (
    echo ✅ EXE создан успешно!
    
    :: Получаем размер файла
    for %%A in ("%OUTPUT_DIR%\HardcoreMessenger.exe") do set SIZE=%%~zA
    set /a SIZE_MB=SIZE/1024/1024
    echo 📏 Размер: ~%SIZE_MB% MB
) else (
    echo ❌ EXE файл не найден!
    pause
    exit /b 1
)

echo.
echo ╔══════════════════════════════════════════════════════════╗
echo ║                  ✅ ГОТОВО!                             ║
echo ╚══════════════════════════════════════════════════════════╝
echo.
echo 📁 Файлы созданы в папке Distribution\
echo.
echo 📋 Содержимое:
dir Distribution /b
echo.
echo 📝 Следующие шаги:
echo    1. Откройте Distribution\ClientConfig.txt
echo    2. Замените YOUR_RAILWAY_URL_HERE на адрес вашего Railway сервера
echo    3. Запакуйте все файлы в ZIP архив
echo    4. Распространяйте ZIP архив среди пользователей!
echo.
echo 💡 Совет: Создайте GitHub Release и прикрепите ZIP архив
echo.
echo 🌟 Новое в версии 2.5 HARD+:
echo    • HARD+ премиум функции
echo    • Эмодзи после ника
echo    • Кастомные темы
echo    • Стикеры и анимации
echo    • Опросы и геолокация
echo.
pause
