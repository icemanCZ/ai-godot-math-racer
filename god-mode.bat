@echo off
rem Builds and starts the game in god mode: holding Shift makes the car five times faster (run.ps1 -GodMode).
rem Extra arguments go to run.ps1, e.g. god-mode.bat -Windowed
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" -GodMode %*
if errorlevel 1 pause
