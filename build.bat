@echo off
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /out:DeadZone.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.ServiceProcess.dll "%~dp0DeadZone.cs"
if errorlevel 1 (echo Build failed.) else (echo Built DeadZone.exe)
pause
