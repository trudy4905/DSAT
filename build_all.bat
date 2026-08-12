@echo off
set "ROOT_DIR=%~dp0"
cd /d "%ROOT_DIR%"

set CONFIG=Debug
set MODE=Fast

if /i "%~1"=="Release" set CONFIG=Release
if /i "%~1"=="Debug"   set CONFIG=Debug
if /i "%~1"=="Full"    set MODE=Full
if /i "%~1"=="All"     set MODE=Full
if /i "%~1"=="Fast"    set MODE=Fast

if /i "%~2"=="Release" set CONFIG=Release
if /i "%~2"=="Debug"   set CONFIG=Debug
if /i "%~2"=="Full"    set MODE=Full
if /i "%~2"=="All"     set MODE=Full
if /i "%~2"=="Fast"    set MODE=Fast

echo ===================================================
echo  WPF (UI) + C++ DLL (Engine) Incremental Build [%CONFIG% / %MODE%]
echo ===================================================

echo [1/2] Building Native C++ Engine DLL...
call NativeEngine\build_dll.bat
cd /d "%ROOT_DIR%"

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: C++ DLL Build failed!
    exit /b 1
)

echo.
echo [2/2] Building WPF Application (%CONFIG% Configuration)...
dotnet build WpfApp\WpfApp.csproj -c %CONFIG% -p:Platform=x64
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: WPF App Build failed!
    exit /b 1
)

if /i "%MODE%"=="Fast" (
    echo.
    echo ===================================================
    echo  SUCCESS: Fast incremental build ready for F5 debugging!
    echo  Executable: WpfApp\bin\x64\%CONFIG%\net10.0-windows\WpfApp.exe
    echo ===================================================
    exit /b 0
)

echo.
echo [3/3] Running Interop Unit Tests...
dotnet test WpfApp.Tests\WpfApp.Tests.csproj -c %CONFIG% -p:Platform=x64
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Unit tests failed!
    exit /b 1
)

echo.
echo ===================================================
echo  SUCCESS: All components compiled and tested!
echo  Executable: WpfApp\bin\x64\%CONFIG%\net10.0-windows\WpfApp.exe
echo ===================================================
