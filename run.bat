@echo off
cd /d "%~dp0"
echo ===================================================
echo  Building and Launching HWP/HWPX Explorer...
echo ===================================================

call build_all.bat

if %ERRORLEVEL% EQU 0 (
    echo Launching application...
    start "" "WpfApp\bin\x64\Release\net10.0-windows\WpfApp.exe"
) else (
    echo Build failed!
    pause
)
