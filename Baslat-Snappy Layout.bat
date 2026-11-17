@echo off
chcp 65001 >nul
taskkill /F /IM "Snappy Layout.exe" >nul 2>&1
taskkill /F /IM "Snappy Lyout.exe" >nul 2>&1
taskkill /F /IM "SnapCraft.exe" >nul 2>&1
timeout /t 1 /nobreak >nul
del /F /Q "%~dp0src\Snappy Layout\bin\Release\net10.0-windows\*.old" >nul 2>&1
rmdir /S /Q "%~dp0..\Snappy Lyout" >nul 2>&1
rmdir /S /Q "%~dp0..\SnapCraft" >nul 2>&1
start "" "%~dp0src\Snappy Layout\bin\Release\net10.0-windows\Snappy Layout.exe"
