@echo off
chcp 65001 >nul
echo.
echo ╔═══════════════════════════════════════════════════════════╗
echo ║   🎨 Создание иконки для HardcoreMessenger               ║
echo ╚═══════════════════════════════════════════════════════════╝
echo.

echo 📝 ИНСТРУКЦИЯ:
echo.
echo Вариант 1 - Использовать готовую картинку:
echo   1. Положите вашу картинку (PNG, JPG) в папку Client\
echo   2. Назовите файл: app_icon.png
echo   3. Запустите этот скрипт снова
echo.
echo Вариант 2 - Создать иконку онлайн:
echo   1. Перейдите на: https://convertio.co/ru/png-ico/
echo   2. Загрузите вашу картинку
echo   3. Скачайте ICO файл
echo   4. Положите в папку Client\ как app_icon.ico
echo.
echo Вариант 3 - Использовать ImageMagick (если установлен):
echo.

cd Client

if exist "app_icon.png" (
    echo ✅ Найден файл app_icon.png
    echo.
    echo Попытка конвертации через ImageMagick...
    
    where magick >nul 2>nul
    if %errorlevel% equ 0 (
        magick convert app_icon.png -define icon:auto-resize=256,128,64,48,32,16 app_icon.ico
        if %errorlevel% equ 0 (
            echo ✅ Иконка успешно создана: app_icon.ico
        ) else (
            echo ❌ Ошибка конвертации
            echo Используйте онлайн конвертер: https://convertio.co/ru/png-ico/
        )
    ) else (
        echo ℹ️ ImageMagick не установлен
        echo.
        echo Используйте онлайн конвертер:
        echo 1. Откройте: https://convertio.co/ru/png-ico/
        echo 2. Загрузите app_icon.png
        echo 3. Скачайте ICO файл в эту папку
    )
) else if exist "app_icon.ico" (
    echo ✅ Найден файл app_icon.ico - всё готово!
) else (
    echo ❌ Файл app_icon.png или app_icon.ico не найден
    echo.
    echo Создайте или скачайте иконку:
    echo.
    echo 🎨 Можете использовать эти сервисы:
    echo   • https://www.favicon-generator.org/
    echo   • https://convertio.co/ru/png-ico/
    echo   • https://www.icoconverter.com/
    echo.
    echo Или создайте простую иконку с текстом:
    echo   • https://www.favicon.cc/
)

cd ..

echo.
echo ═══════════════════════════════════════════════════════════
echo.
echo 💡 СОВЕТ: Размер иконки должен быть:
echo    • Минимум: 256x256 пикселей
echo    • Рекомендуется: 512x512 или 1024x1024
echo    • Формат: PNG (прозрачный фон лучше)
echo.
echo После создания иконки запустите BUILD_EXE.bat
echo.
pause
