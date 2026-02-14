@echo off
chcp 65001 >nul
cls

:MENU
cls
echo.
echo ================================================================
echo.
echo         HARDCORE MESSENGER - Master Setup
echo                ULTIMATE v2.5 Edition
echo.
echo ================================================================
echo.
echo  Select action:
echo.
echo  [1] Full setup (GitHub + Railway + EXE)
echo  [2] Build EXE file only
echo  [3] Setup GitHub only
echo  [4] Run client (locally)
echo  [5] Run server (locally)
echo  [6] Create application icon
echo  [7] Open step-by-step guide
echo  [8] Help and support
echo  [0] Exit
echo.
set /p choice="Enter number: "

if "%choice%"=="1" goto FULL_SETUP
if "%choice%"=="2" goto BUILD_EXE
if "%choice%"=="3" goto SETUP_GITHUB
if "%choice%"=="4" goto RUN_CLIENT
if "%choice%"=="5" goto RUN_SERVER
if "%choice%"=="6" goto CREATE_ICON
if "%choice%"=="7" goto OPEN_GUIDE
if "%choice%"=="8" goto HELP
if "%choice%"=="0" goto EXIT
goto MENU

:FULL_SETUP
cls
echo ================================================================
echo           FULL SETUP - STEP BY STEP PROCESS
echo ================================================================
echo.
echo This process includes:
echo  - GitHub repository setup
echo  - Railway deployment preparation
echo  - EXE file creation
echo.
echo Time required: ~15-20 minutes
echo.
echo IMPORTANT: Make sure you have installed:
echo  - Git (https://git-scm.com/download/win)
echo  - .NET 8.0 SDK (https://dotnet.microsoft.com/download/dotnet/8.0)
echo  - GitHub account
echo  - Railway account
echo.
pause

echo.
echo ================================================================
echo  STAGE 1/3: GitHub Setup
echo ================================================================
call SETUP_GITHUB.bat
if %errorlevel% neq 0 (
    echo [ERROR] GitHub setup failed
    pause
    goto MENU
)

echo.
echo ================================================================
echo  STAGE 2/3: Railway Instructions
echo ================================================================
echo.
echo Open in browser: https://railway.app
echo.
echo Follow instructions from STEP_BY_STEP_GUIDE.txt
echo Section "STAGE 4: RAILWAY DEPLOYMENT"
echo.
echo After deployment on Railway, press any key...
pause

echo.
echo ================================================================
echo  STAGE 3/3: Building EXE
echo ================================================================
call BUILD_EXE.bat
if %errorlevel% neq 0 (
    echo [ERROR] EXE build failed
    pause
    goto MENU
)

echo.
echo ================================================================
echo                    ALL DONE!
echo ================================================================
echo.
echo Your files are in Distribution\ folder
echo.
echo Next steps:
echo  1. Open Distribution\ClientConfig.txt
echo  2. Replace YOUR_RAILWAY_URL_HERE with your Railway URL
echo  3. Create ZIP archive from Distribution folder
echo  4. Distribute your messenger!
echo.
echo Detailed instructions: STEP_BY_STEP_GUIDE.txt
echo.
pause
goto MENU

:BUILD_EXE
call BUILD_EXE.bat
pause
goto MENU

:SETUP_GITHUB
call SETUP_GITHUB.bat
pause
goto MENU

:RUN_CLIENT
cls
echo ================================================================
echo                   Running Client
echo ================================================================
echo.
echo Make sure that:
echo  1. Server is running (locally or on Railway)
echo  2. ClientConfig.txt contains correct address
echo.
pause
call START_CLIENT.bat
goto MENU

:RUN_SERVER
cls
echo ================================================================
echo                   Running Server
echo ================================================================
echo.
echo Server will start on port 8080
echo Clients can connect to: ws://localhost:8080
echo.
pause
call START_SERVER.bat
goto MENU

:CREATE_ICON
call CREATE_ICON.bat
pause
goto MENU

:OPEN_GUIDE
cls
echo ================================================================
echo            Opening Step-by-Step Guide
echo ================================================================
echo.
if exist "STEP_BY_STEP_GUIDE.txt" (
    echo Opening STEP_BY_STEP_GUIDE.txt...
    start notepad STEP_BY_STEP_GUIDE.txt
    echo.
    echo [OK] File opened in Notepad
) else (
    echo [ERROR] File STEP_BY_STEP_GUIDE.txt not found!
)
echo.
pause
goto MENU

:HELP
cls
echo ================================================================
echo                       HELP
echo ================================================================
echo.
echo Available guides:
echo.
echo  * START_HERE.txt           - START WITH THIS!
echo  * STEP_BY_STEP_GUIDE.txt   - Main detailed guide
echo  1. ALL_FEATURES_LIST.md    - List of all 56 features
echo  2. HARD_PLUS_GUIDE.md      - HARD+ premium guide
echo  3. HARD_PLUS_FEATURES.txt  - Infographic
echo  4. README_HARD_PLUS.md     - General info
echo  5. CHANGELOG_HARD_PLUS.md  - Version history
echo.
echo Useful links:
echo.
echo  - Railway.app: https://railway.app
echo  - GitHub: https://github.com
echo  - .NET SDK: https://dotnet.microsoft.com/download/dotnet/8.0
echo  - Git: https://git-scm.com/download/win
echo.
echo Quick start:
echo.
echo  Step 1: Install Git and .NET 8.0 SDK
echo  Step 2: Create GitHub and Railway accounts
echo  Step 3: Run option [1] in main menu
echo  Step 4: Follow instructions
echo.
echo Common problems:
echo.
echo  Q: Git not found
echo  A: Install Git and restart computer
echo.
echo  Q: dotnet not found
echo  A: Install .NET 8.0 SDK (not Runtime!)
echo.
echo  Q: Client cannot connect
echo  A: Check ClientConfig.txt and Railway server
echo.
echo Full guide: STEP_BY_STEP_GUIDE.txt (40 minutes)
echo.
pause
goto MENU

:EXIT
cls
echo.
echo Thank you for using HardcoreMessenger!
echo.
echo Don't forget to:
echo  - Read STEP_BY_STEP_GUIDE.txt
echo  - Setup Railway for online access
echo  - Share messenger with friends!
echo.
echo All HARD+ features are FREE!
echo.
echo See you! 
timeout /t 3 >nul
exit
