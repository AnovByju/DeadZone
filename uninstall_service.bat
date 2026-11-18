@echo off
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
sc stop DeadZoneSvc
sc delete DeadZoneSvc
pause
