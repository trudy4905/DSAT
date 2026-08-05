@echo off
echo Building NativeEngine.dll using Visual C++ toolchain (OOP Modular Architecture)...

call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"

cd /d "%~dp0"

cl.exe /LD /DENGINE_EXPORTS /utf-8 /O2 /EHsc /W4 /std:c++17 ^
  Engine.cpp ^
  Analyzers\DocumentAnalyzerBase.cpp ^
  Analyzers\HwpDocumentAnalyzer.cpp ^
  Analyzers\Hwp\CheckHwpOleOverlay.cpp ^
  Analyzers\Hwp\CheckHwpMacro.cpp ^
  Analyzers\Hwp\CheckHwpStructure.cpp ^
  Analyzers\HwpxDocumentAnalyzer.cpp ^
  Analyzers\Hwpx\CheckHwpxZipOverlay.cpp ^
  Analyzers\PdfDocumentAnalyzer.cpp ^
  Analyzers\Pdf\CheckPdfEofOverlay.cpp ^
  Factories\DocumentAnalyzerFactory.cpp ^
  Managers\EngineStatusManager.cpp ^
  /Fe:NativeEngine.dll

if %ERRORLEVEL% EQU 0 (
    echo SUCCESS: NativeEngine.dll built successfully.
) else (
    echo ERROR: Build failed.
    exit /b 1
)
