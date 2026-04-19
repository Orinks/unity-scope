@echo off
REM Build UnityScope BepInEx plugin
REM Usage:
REM   build.bat           - Debug build (output in src/UnityScope.Runtime/bin/Debug)
REM   build.bat release   - Release build (output directly to game's BepInEx/plugins folder)

set CONFIG=Debug
if /i "%1"=="release" set CONFIG=Release

echo Building UnityScope (%CONFIG%)...
dotnet build src\UnityScope.Runtime\UnityScope.Runtime.csproj -c %CONFIG%

if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    exit /b 1
)

echo.
if /i "%CONFIG%"=="Release" (
    echo Built and copied to game BepInEx\plugins\UnityScope\ folder.
) else (
    echo Built to src\UnityScope.Runtime\bin\Debug\net472\
    echo Run "build.bat release" to deploy directly into the game.
)
