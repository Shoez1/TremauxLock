@echo off
echo ============================================================
echo  TremauxLock - Release Build (Single-File Portable EXE)
echo ============================================================

set PROJECT=src\TremauxLock.App\TremauxLock.App.csproj
set OUTDIR=dist\wpf-win-x64

dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:IncludeAllContentForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:GenerateRuntimeConfigurationFiles=false /p:PublishReadyToRun=false /p:DebugType=None /p:DebugSymbols=false -o "%OUTDIR%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERRO] Build falhou com codigo %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)

if exist "%OUTDIR%\*.pdb" del /q "%OUTDIR%\*.pdb"
if exist "%OUTDIR%\*.dll" del /q "%OUTDIR%\*.dll"

echo.
echo ============================================================
echo  [OK] Executavel portatil gerado com sucesso:
echo       %OUTDIR%\TremauxLock.exe
echo ============================================================