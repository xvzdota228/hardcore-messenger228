@echo off
chcp 65001 >nul
cls
echo.
echo ================================================================
echo.
echo    HARDCORE MESSENGER - GitHub Setup
echo.
echo ================================================================
echo.
echo This script will help you setup GitHub repository
echo for deployment on Railway.app
echo.
pause

echo.
echo [STEP 1/4] Checking Git installation...
where git >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] Git not found!
    echo.
    echo Please install Git from:
    echo https://git-scm.com/download/win
    echo.
    echo After installation, restart this script.
    pause
    exit /b 1
)
echo [OK] Git is installed

echo.
echo [STEP 2/4] Enter your GitHub details...
echo.
set /p USERNAME="Enter your GitHub username: "
set /p REPO_NAME="Enter repository name (example: hardcore-messenger): "

echo.
echo [STEP 3/4] Initializing Git repository...

if not exist .git (
    git init
    echo [OK] Repository initialized
) else (
    echo [INFO] Repository already initialized
)

git add .
echo [OK] Files added

git commit -m "Initial commit: HardcoreMessenger v2.5 HARD+ Edition"
if %errorlevel% neq 0 (
    echo [INFO] No changes to commit or commit already created
)

git branch -M main
echo [OK] Branch 'main' created

echo.
echo [STEP 4/4] Connecting to GitHub...

git remote remove origin 2>nul
git remote add origin https://github.com/%USERNAME%/%REPO_NAME%.git
echo [OK] Remote added

echo.
echo ================================================================
echo                 IMPORTANT - NEXT STEPS
echo ================================================================
echo.
echo 1. Create repository on GitHub:
echo    https://github.com/new
echo.
echo 2. Repository name: %REPO_NAME%
echo    Description: Secure messenger with HARD+ premium features
echo    Type: Public
echo.
echo 3. DO NOT add README, .gitignore or license
echo    (they already exist in the project)
echo.
echo 4. After creating repository on GitHub, press any key
echo    to upload the code...
echo.
pause

echo.
echo [UPLOADING] Pushing code to GitHub...
git push -u origin main

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Failed to push!
    echo.
    echo Possible reasons:
    echo 1. Repository not created on GitHub
    echo 2. Wrong username or repository name
    echo 3. Authentication required
    echo.
    echo Try pushing manually:
    echo git push -u origin main
    echo.
    pause
    exit /b 1
)

echo.
echo ================================================================
echo                     SUCCESS!
echo ================================================================
echo.
echo Your code is uploaded to GitHub!
echo.
echo Repository URL:
echo    https://github.com/%USERNAME%/%REPO_NAME%
echo.
echo.
echo ================================================================
echo              NEXT STEP: RAILWAY.APP
echo ================================================================
echo.
echo 1. Open https://railway.app
echo.
echo 2. Click "Start a New Project"
echo.
echo 3. Select "Deploy from GitHub repo"
echo.
echo 4. Find repository: %REPO_NAME%
echo.
echo 5. Railway will automatically deploy the server!
echo.
echo 6. Go to Settings - Networking - Generate Domain
echo.
echo 7. Copy URL (example: hardcore-messenger.up.railway.app)
echo.
echo 8. Use this URL in client:
echo    ws://hardcore-messenger.up.railway.app
echo.
echo.
echo Full instructions: STEP_BY_STEP_GUIDE.txt
echo.
pause
