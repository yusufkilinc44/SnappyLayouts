@echo off
chcp 65001 >nul
taskkill /F /IM "Snappy Layouts.exe" >nul 2>&1
timeout /t 1 /nobreak >nul
del /F /Q "%~dp0src\Snappy Layouts\bin\Release\net10.0-windows\*.old" >nul 2>&1
start "" "%~dp0src\Snappy Layouts\bin\Release\net10.0-windows\Snappy Layouts.exe"
