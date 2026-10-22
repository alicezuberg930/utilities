@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-converter-workers.ps1" %*
exit /b %ERRORLEVEL%
