@echo off
REM Build UnityScope BepInEx plugin.
REM
REM Usage:
REM   build.bat release "C:\Path\To\Game"
REM     - Builds Release and deploys directly into <GameDir>\BepInEx\plugins\UnityScope\.
REM   build.bat debug "C:\Path\To\Game"
REM     - Builds Debug into src\UnityScope.Runtime\bin\Debug\.
REM   build.bat
REM     - If %UNITYSCOPE_GAMEDIR% is set in your environment, uses that.
REM       Otherwise prints help and exits.

setlocal

set CONFIG=Debug
if /i "%~1"=="release" (
    set CONFIG=Release
    shift
) else if /i "%~1"=="debug" (
    set CONFIG=Debug
    shift
)

set GAMEDIR=%~1
if "%GAMEDIR%"=="" set GAMEDIR=%UNITYSCOPE_GAMEDIR%

if "%GAMEDIR%"=="" (
    echo.
    echo ERROR: GameDir required. Either:
    echo   build.bat release "C:\Program Files (x86)\Steam\steamapps\common\YourGame"
    echo   set UNITYSCOPE_GAMEDIR=C:\... ^&^& build.bat release
    echo.
    exit /b 1
)

echo Building UnityScope (%CONFIG%) for: %GAMEDIR%
dotnet build src\UnityScope.Runtime\UnityScope.Runtime.csproj -c %CONFIG% -p:GameDir="%GAMEDIR%"

if %ERRORLEVEL% NEQ 0 (
    echo Build failed.
    exit /b 1
)

if /i "%CONFIG%"=="Release" (
    echo.
    echo Built and deployed to: %GAMEDIR%\BepInEx\plugins\UnityScope\
)

endlocal
