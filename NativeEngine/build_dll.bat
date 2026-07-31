@echo off
echo Building NativeEngine.dll using Visual C++ toolchain (OOP Modular Architecture)...

call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"

cd /d "%~dp0"

cl.exe /LD /DENGINE_EXPORTS /utf-8 /O2 /EHsc /W4 /std:c++17 ^
  Engine.cpp ^
  Analyzers\DocumentAnalyzerBase.cpp ^
  Analyzers\HwpDocumentAnalyzer.cpp ^
  Analyzers\HwpxDocumentAnalyzer.cpp ^
  Analyzers\PdfDocumentAnalyzer.cpp ^
  Factories\DocumentAnalyzerFactory.cpp ^
  Managers\EngineStatusManager.cpp ^
  /Fe:NativeEngine.dll

if %ERRORLEVEL% EQU 0 (
    echo SUCCESS: NativeEngine.dll built successfully.
) else (
    echo ERROR: Build failed.
    exit /b 1
)
