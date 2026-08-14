@echo off
set "ENGINE_DIR=%~dp0"
cd /d "%ENGINE_DIR%"

set OUT_DIR=%ENGINE_DIR%bin
if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"

set CONFIG_MODE=Debug
if /i "%~1"=="Release" set CONFIG_MODE=Release

REM Check if any .cpp or .h files are newer than existing NativeEngine.dll and if build configuration changed
powershell -NoProfile -Command "$e='%ENGINE_DIR%'.TrimEnd('\'); $o='%OUT_DIR%'.TrimEnd('\'); $d=Join-Path $o 'NativeEngine.dll'; $c=Join-Path $o 'config.txt'; if(-not(Test-Path $d) -or -not(Test-Path $c)){exit 1}; if((Get-Content $c -Raw).Trim() -ne '%CONFIG_MODE%'){exit 1}; $dt=(Get-Item $d).LastWriteTime; foreach($f in Get-ChildItem $e -Recurse -Include *.cpp,*.h){if($f.LastWriteTime -gt $dt){exit 1}}; exit 0;" >nul 2>&1

if %ERRORLEVEL% EQU 0 (
    echo [NativeEngine] C++ components up to date - %CONFIG_MODE% mode. Skipping re-compilation.
    exit /b 0
)

echo Building NativeEngine.dll - %CONFIG_MODE% Configuration...

call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1

set THIRD_PARTY=%ENGINE_DIR%3rdparty
set LIBEWF_INC=%THIRD_PARTY%\libewf\include
set LIBEWF_LIB=%THIRD_PARTY%\libewf\lib\x64
set LIBEWF_BIN=%THIRD_PARTY%\libewf\bin\x64
set TSK_INC=%THIRD_PARTY%\sleuthkit\include
set TSK_LIB=%THIRD_PARTY%\sleuthkit\lib\x64
set TSK_BIN=%THIRD_PARTY%\sleuthkit\bin\x64

if /i "%~1"=="Release" (
    echo [NativeEngine] Compiling C++ Engine in Release Mode - Full O2 Speed Optimization...
    cl.exe /LD /DNATIVEENGINE_EXPORTS /DENGINE_EXPORTS /utf-8 /EHsc /W4 /wd4201 /std:c++17 /O2 /DNDEBUG ^
      /I. ^
      /I"%LIBEWF_INC%" ^
      /I"%TSK_INC%" ^
      NativeEngineApi.cpp ^
      ImageReaders\E01ImageReader.cpp ^
      ImageReaders\DdImageReader.cpp ^
      ImageReaders\ImageReaderFactory.cpp ^
      FileSystem\TskFileSystemModule.cpp ^
      Analyzers\DocumentAnalyzerBase.cpp ^
      Analyzers\HwpDocumentAnalyzer.cpp ^
      Analyzers\Hwp\CheckHwpOleOverlay.cpp ^
      Analyzers\Hwp\CheckHwpOleSlack.cpp ^
      Analyzers\Hwp\CheckHwpMacro.cpp ^
      Analyzers\HwpxDocumentAnalyzer.cpp ^
      Analyzers\Hwpx\CheckHwpxZipOverlay.cpp ^
      Analyzers\PdfDocumentAnalyzer.cpp ^
      Analyzers\Pdf\CheckPdfEofOverlay.cpp ^
      Factories\DocumentAnalyzerFactory.cpp ^
      Managers\EngineStatusManager.cpp ^
      /Fo"%OUT_DIR%/" ^
      /link ^
      /DLL ^
      /LTCG ^
      /ignore:4099 ^
      /LIBPATH:"%LIBEWF_LIB%" ^
      /LIBPATH:"%TSK_LIB%" ^
      libewf.lib ^
      libtsk.lib ^
      ole32.lib ^
      /IMPLIB:"%OUT_DIR%\NativeEngine.lib" ^
      /OUT:"%OUT_DIR%\NativeEngine.dll"
) else (
    echo [NativeEngine] Compiling C++ Engine in Debug Mode...
    cl.exe /LD /DNATIVEENGINE_EXPORTS /DENGINE_EXPORTS /utf-8 /EHsc /W4 /wd4201 /std:c++17 /Zi /Od /RTC1 ^
      /I. ^
      /I"%LIBEWF_INC%" ^
      /I"%TSK_INC%" ^
      NativeEngineApi.cpp ^
      ImageReaders\E01ImageReader.cpp ^
      ImageReaders\DdImageReader.cpp ^
      ImageReaders\ImageReaderFactory.cpp ^
      FileSystem\TskFileSystemModule.cpp ^
      Analyzers\DocumentAnalyzerBase.cpp ^
      Analyzers\HwpDocumentAnalyzer.cpp ^
      Analyzers\Hwp\CheckHwpOleOverlay.cpp ^
      Analyzers\Hwp\CheckHwpOleSlack.cpp ^
      Analyzers\Hwp\CheckHwpMacro.cpp ^
      Analyzers\HwpxDocumentAnalyzer.cpp ^
      Analyzers\Hwpx\CheckHwpxZipOverlay.cpp ^
      Analyzers\PdfDocumentAnalyzer.cpp ^
      Analyzers\Pdf\CheckPdfEofOverlay.cpp ^
      Factories\DocumentAnalyzerFactory.cpp ^
      Managers\EngineStatusManager.cpp ^
      /Fo"%OUT_DIR%/" ^
      /Fd"%OUT_DIR%\vc140.pdb" ^
      /link ^
      /DLL ^
      /DEBUG ^
      /LTCG ^
      /ignore:4099 ^
      /PDB:"%OUT_DIR%\NativeEngine.pdb" ^
      /LIBPATH:"%LIBEWF_LIB%" ^
      /LIBPATH:"%TSK_LIB%" ^
      libewf.lib ^
      libtsk.lib ^
      ole32.lib ^
      /IMPLIB:"%OUT_DIR%\NativeEngine.lib" ^
      /OUT:"%OUT_DIR%\NativeEngine.dll"
)

if %ERRORLEVEL% EQU 0 (
    echo %CONFIG_MODE% > "%OUT_DIR%\config.txt"
    echo SUCCESS: NativeEngine.dll built successfully into bin folder - %CONFIG_MODE% mode
    if exist "%LIBEWF_BIN%\libewf.dll"  copy /Y "%LIBEWF_BIN%\libewf.dll"  "%OUT_DIR%\" >nul
    if exist "%ZLIB_BIN%\zlib.dll"    copy /Y "%ZLIB_BIN%\zlib.dll"    "%OUT_DIR%\" >nul
    if exist "%TSK_BIN%\libtsk_jni.dll" copy /Y "%TSK_BIN%\libtsk_jni.dll" "%OUT_DIR%\" >nul
) else (
    echo ERROR: Build failed.
    exit /b 1
)
