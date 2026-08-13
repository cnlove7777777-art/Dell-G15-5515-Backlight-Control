@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install.ps1"
set "G15_EXIT=%ERRORLEVEL%"
echo.
if not "%G15_EXIT%"=="0" echo Installation did not complete. Review the message above.
pause
exit /b %G15_EXIT%
