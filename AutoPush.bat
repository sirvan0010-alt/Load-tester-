@echo off
setlocal
title Universal AutoPush

cd /d "%~dp0"

echo ==========================================
echo          UNIVERSAL AUTOPUSH
echo ==========================================
echo.
echo Spoustim AutoPush.ps1...
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0AutoPush.ps1"

echo.
echo ==========================================
echo AutoPush skoncil.
echo ==========================================
pause
