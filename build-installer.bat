@echo off
REM Builds Interrogon.Installer\bin\Release\Interrogon.Installer.msi from scratch.
REM Publishes Interrogon.App as a self-contained win-x64 executable into
REM Interrogon.Installer\staging\App\, then builds the MSI from that staging folder.
REM staging\ is git-ignored generated output, not committed.
REM
REM Run this whenever you want to cut a new installer build.

setlocal enabledelayedexpansion

set STAGING=%~dp0Interrogon.Installer\staging\App

echo Reading version from Interrogon.App.csproj...
for /f "usebackq tokens=3 delims=<>" %%V in (`findstr /c:"<Version>" "%~dp0src\Interrogon.App\Interrogon.App.csproj"`) do set PRODUCT_VERSION=%%V
if "%PRODUCT_VERSION%"=="" goto :error_version
echo Version: %PRODUCT_VERSION%

echo Cleaning previous staging output...
if exist "%STAGING%" rmdir /s /q "%STAGING%"

echo.
echo Publishing Interrogon...
dotnet publish "%~dp0src\Interrogon.App\Interrogon.App.csproj" -c Release -r win-x64 --self-contained true -p:Platform=x64 -o "%STAGING%"
if errorlevel 1 goto :error

echo.
echo Copying ReadMe.txt into the install payload...
copy /y "%~dp0Interrogon.Installer\ReadMe.txt" "%STAGING%\ReadMe.txt" >nul

echo.
echo Building installer (version %PRODUCT_VERSION%)...
dotnet build "%~dp0Interrogon.Installer\Interrogon.Installer.wixproj" -c Release -p:DefineConstants=ProductVersion=%PRODUCT_VERSION%
if errorlevel 1 goto :error

echo.
echo Done. Installer at Interrogon.Installer\bin\Release\Interrogon.Installer.msi
exit /b 0

:error
echo.
echo Build failed.
exit /b 1

:error_version
echo.
echo Could not read ^<Version^> from src\Interrogon.App\Interrogon.App.csproj.
exit /b 1
