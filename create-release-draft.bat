@echo off
setlocal enabledelayedexpansion
set GH_REPO=mrTorch222/PatternExporterNX

echo ========================================
echo PatternExporterNX - Release Draft Creator
echo ========================================
echo.

:: Check if gh CLI is available
where gh >nul 2>&1
if errorlevel 1 (
    echo [ERROR] GitHub CLI (gh^) not found. Please install it from https://cli.github.com/
    pause
    exit /b 1
)

:: Extract VersionPrefix from .csproj
echo [INFO] Extracting version from project file...
for /f "tokens=3 delims=<>" %%i in ('findstr /C:"<VersionPrefix>" FlatPatternExporter\FlatPatternExporter.csproj') do set VERSION_PREFIX=%%i

if not defined VERSION_PREFIX (
    echo [ERROR] Could not extract VersionPrefix from .csproj file
    pause
    exit /b 1
)

:: Get revision number from Git commits
for /f "tokens=*" %%i in ('git rev-list --count HEAD') do set COMMIT_COUNT=%%i

:: Combine into full version
set VERSION=%VERSION_PREFIX%.%COMMIT_COUNT%

echo [INFO] Detected version: v%VERSION%
echo [INFO] Version prefix: %VERSION_PREFIX%
echo [INFO] Git commit count: %COMMIT_COUNT%
echo.

:: Check if Release directory exists and has archives
if exist "Release\" (
    set RELEASE_EXISTS=1
) else (
    echo [ERROR] Release directory not found. Run publish.bat first.
    pause
    exit /b 1
)

set MAIN_ARCHIVE_COUNT=0
for %%f in (Release\PatternExporterNX-v%VERSION%-x64-*.zip) do (
    if exist "%%f" set /a MAIN_ARCHIVE_COUNT+=1
)

if %MAIN_ARCHIVE_COUNT% equ 0 (
    echo [ERROR] No main application archive found for v%VERSION%.
    echo Run publish.bat with the required application profile first.
    pause
    exit /b 1
)

if not exist "Release\PatternExporterNX.Updater-v%VERSION%-x64.zip" (
    echo [ERROR] Updater archive is missing for v%VERSION%.
    echo Run: publish.bat updater
    pause
    exit /b 1
)

set /a ZIP_COUNT=MAIN_ARCHIVE_COUNT+1

echo [INFO] Found %ZIP_COUNT% archive(s^) for v%VERSION%
echo.

:: List archives
echo Archives to upload:
for %%f in (Release\PatternExporterNX-v%VERSION%-x64-*.zip Release\PatternExporterNX.Updater-v%VERSION%-x64.zip) do (
    echo   - %%~nxf
)
echo.
echo [SUCCESS] Archive versions match: v%VERSION%
echo.

:: Confirm action
set /p CONFIRM="Create draft release v%VERSION%? (y/n): "
if /i "%CONFIRM%" neq "y" (
    echo Cancelled.
    exit /b 0
)
echo.

:: Check if tag already exists (local and remote)
set TAG_EXISTS_LOCAL=0
set TAG_EXISTS_REMOTE=0

git rev-parse v%VERSION% >nul 2>&1
if %errorlevel% equ 0 set TAG_EXISTS_LOCAL=1

for /f %%i in ('git ls-remote --tags origin refs/tags/v%VERSION% 2^>nul ^| find "refs/tags/"') do set TAG_EXISTS_REMOTE=1

if %TAG_EXISTS_LOCAL% equ 1 (
    echo [WARNING] Tag v%VERSION% already exists locally
)
if %TAG_EXISTS_REMOTE% equ 1 (
    echo [WARNING] Tag v%VERSION% already exists on remote
)

if %TAG_EXISTS_LOCAL% equ 1 (
    set /p DELETE_TAG="Delete and recreate tag? (y/n): "
    if /i "!DELETE_TAG!" neq "y" (
        echo Cancelled.
        exit /b 0
    )

    if %TAG_EXISTS_LOCAL% equ 1 (
        echo [INFO] Deleting local tag v%VERSION%...
        git tag -d v%VERSION%
        if errorlevel 1 (
            echo [ERROR] Failed to delete local tag
            pause
            exit /b 1
        )
    )

    if %TAG_EXISTS_REMOTE% equ 1 (
        echo [INFO] Deleting remote tag v%VERSION%...
        git push origin --delete v%VERSION%
        if errorlevel 1 (
            echo [ERROR] Failed to delete remote tag
            pause
            exit /b 1
        )
    )
) else (
    if %TAG_EXISTS_REMOTE% equ 1 (
        set /p DELETE_TAG="Delete remote tag and create new? (y/n): "
        if /i "!DELETE_TAG!" neq "y" (
            echo Cancelled.
            exit /b 0
        )

        echo [INFO] Deleting remote tag v%VERSION%...
        git push origin --delete v%VERSION%
        if errorlevel 1 (
            echo [ERROR] Failed to delete remote tag
            pause
            exit /b 1
        )
    )
)
echo.

:: Check if release already exists on GitHub
echo [INFO] Checking for existing release on GitHub...
gh release view v%VERSION% >nul 2>&1
if %errorlevel% equ 0 (
    echo [WARNING] Release v%VERSION% already exists on GitHub
    set /p DELETE_RELEASE="Delete existing release and create new? (y/n): "
    if /i "!DELETE_RELEASE!" neq "y" (
        echo Cancelled.
        exit /b 0
    )

    echo [INFO] Deleting existing release v%VERSION%...
    gh release delete v%VERSION% --yes
    if errorlevel 1 (
        echo [ERROR] Failed to delete existing release
        pause
        exit /b 1
    )
    echo [SUCCESS] Existing release deleted
) else (
    echo [INFO] No existing release found
)
echo.

:: Create tag
echo [STEP 1/4] Creating tag v%VERSION%...
git tag -a v%VERSION% -m "Release %VERSION%"
if errorlevel 1 (
    echo [ERROR] Failed to create tag
    pause
    exit /b 1
)
echo [SUCCESS] Tag created
echo.

:: Push tag
echo [STEP 2/4] Pushing tag to GitHub...
git push origin v%VERSION%
if errorlevel 1 (
    echo [ERROR] Failed to push tag
    pause
    exit /b 1
)
echo [SUCCESS] Tag pushed
echo.

:: Create draft release with archives
echo [STEP 3/4] Creating draft release on GitHub...
echo [INFO] This may take a few minutes to upload archives...
echo.

:: Create temporary release notes file
set NOTES_FILE=%TEMP%\release_notes_%VERSION%.md
(
echo ## Release Notes
echo.
echo PatternExporterNX is an independent MIT-licensed fork of https://github.com/isinicyn/FlatPatternExporter.
echo Original author: Sinicyn Ivan Victorovich. Fork maintainer: mrTorch222.
echo LICENSE.txt and NOTICE.md are included in each archive.
echo.
echo Please add release notes and verification results here.
echo.
echo ### Archives
echo.
echo - **Deploy** - For installers ^(Inno Setup, WiX, NSIS^)
echo - **Portable** - Single executable, no installation required
echo - **FrameworkDependent** - Requires .NET 10.0 Runtime
echo - **Updater** - For automatic updates
) > "%NOTES_FILE%"

gh release create v%VERSION% --draft --title "v%VERSION%" --notes-file "%NOTES_FILE%" Release\PatternExporterNX-v%VERSION%-x64-*.zip Release\PatternExporterNX.Updater-v%VERSION%-x64.zip

:: Clean up temporary file
del "%NOTES_FILE%" 2>nul

if errorlevel 1 (
    echo [ERROR] Failed to create draft release
    echo You may need to delete the tag manually: git tag -d v%VERSION% ^&^& git push origin --delete v%VERSION%
    pause
    exit /b 1
)

echo [SUCCESS] Draft release created
echo.

:: Get release URL
echo [STEP 4/4] Getting release URL...
for /f "tokens=*" %%i in ('gh release view v%VERSION% --json url -q .url') do set RELEASE_URL=%%i

echo.
echo ========================================
echo Draft Release Created Successfully!
echo ========================================
echo.
echo Version: v%VERSION%
echo Tag: v%VERSION%
echo Status: DRAFT
echo.
echo Release URL:
echo %RELEASE_URL%
echo.
echo Edit URL:
echo %RELEASE_URL%/edit
echo.
echo [NEXT STEPS]
echo 1. Open the edit URL in your browser
echo 2. Update release notes with detailed description
echo 3. Click "Publish release" when ready
echo.
pause
