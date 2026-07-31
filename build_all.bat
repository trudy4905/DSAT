@echo off
echo ===================================================
echo  WPF (UI) + C++ DLL (Engine) Full Solution Build
echo ===================================================

set "ROOT_DIR=%~dp0"
cd /d "%ROOT_DIR%"

echo [1/3] Building Native C++ Engine DLL...
call NativeEngine\build_dll.bat
cd /d "%ROOT_DIR%"

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: C++ DLL Build failed!
    exit /b 1
)

echo.
echo [2/3] Building WPF Application...
dotnet build WpfApp\WpfApp.csproj -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: WPF App Build failed!
    exit /b 1
)

echo.
echo [3/3] Running Interop Tests...
dotnet test WpfApp.Tests\WpfApp.Tests.csproj
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Unit tests failed!
    exit /b 1
)

echo.
echo ===================================================
echo  SUCCESS: All components compiled and tested!
echo  Executable: WpfApp\bin\Release\net10.0-windows\WpfApp.exe
echo ===================================================
