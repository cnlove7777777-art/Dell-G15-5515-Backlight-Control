@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Uninstall.ps1"
set "G15_EXIT=%ERRORLEVEL%"
echo.
pause
exit /b %G15_EXIT%
