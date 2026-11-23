@echo off
rem Builds and starts the game fullscreen (run.ps1). Extra arguments go to run.ps1, e.g. run.bat -Windowed
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*
if errorlevel 1 pause
