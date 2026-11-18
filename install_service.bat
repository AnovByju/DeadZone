@echo off
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
cd /d "%~dp0"
if not exist "%~dp0DeadZone.exe" (echo DeadZone.exe not found next to this file. Run build.bat first. & pause & exit /b)
sc stop DeadZoneSvc >nul 2>&1
sc delete DeadZoneSvc >nul 2>&1
sc create DeadZoneSvc binPath= "\"%~dp0DeadZone.exe\" --service" start= auto DisplayName= "DeadZone lock screen helper"
sc description DeadZoneSvc "Shows the lock screen and sign-in screen on the working part of the laptop panel."
sc start DeadZoneSvc
echo.
echo Done. Keep DeadZone.exe in this folder (do not move or delete it).
pause
