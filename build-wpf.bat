@echo off
echo ============================================================
echo  TremauxLock WPF — Release Build
echo ============================================================

dotnet publish src\TremauxLock.App\TremauxLock.App.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:EnableCompressionInSingleFile=true ^
    /p:DebugType=None ^
    /p:DebugSymbols=false ^
    -o dist\wpf-win-x64

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERRO] Build falhou.
    exit /b %ERRORLEVEL%
)

echo.
echo [OK] Build concluido: dist\wpf-win-x64\TremauxLock.exe
