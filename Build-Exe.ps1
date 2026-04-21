param(
    [string]$Project = "LockerApp.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$projectPath = Join-Path $projectRoot $Project
$logFile = Join-Path $projectRoot "build-exe.log"

[System.IO.File]::WriteAllText($logFile, "")

function Write-Log {
    param(
        [string]$Level,
        [string]$Message
    )

    $line = "[{0}] {1}" -f $Level, $Message
    Write-Host $line
    Add-Content -Path $logFile -Value $line
}

function Invoke-And-Log {
    param(
        [scriptblock]$Action
    )

    & $Action 2>&1 | ForEach-Object {
        $text = $_.ToString()
        Write-Host $text
        Add-Content -Path $logFile -Value $text
    }
}

function Get-ProjectValue {
    param(
        [string]$CsprojPath,
        [string]$PropertyName
    )

    [xml]$projectXml = Get-Content -LiteralPath $CsprojPath
    return $projectXml.Project.PropertyGroup.$PropertyName | Select-Object -First 1
}

try {
    Write-Log "INFO" ("Iniciando build em {0}" -f $projectRoot)

    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw ("Projeto '{0}' nao encontrado." -f $Project)
    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "O .NET SDK nao foi encontrado no PATH. Instale o .NET 9 SDK ou superior."
    }

    $targetFramework = Get-ProjectValue -CsprojPath $projectPath -PropertyName "TargetFramework"
    if ([string]::IsNullOrWhiteSpace($targetFramework)) {
        throw "Nao foi possivel identificar o TargetFramework no arquivo do projeto."
    }

    $assemblyName = Get-ProjectValue -CsprojPath $projectPath -PropertyName "AssemblyName"
    if ([string]::IsNullOrWhiteSpace($assemblyName)) {
        $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
    }

    # Pasta fixa e limpa: só o .exe após o publish (ficheiros auxiliares são removidos)
    $publishDir = Join-Path $projectRoot ("dist\{0}" -f $RuntimeIdentifier)
    $outputExe = Join-Path $publishDir ("{0}.exe" -f $assemblyName)

    Write-Log "INFO" "Executando dotnet restore..."
    Invoke-And-Log { & dotnet restore $projectPath -r $RuntimeIdentifier }
    if ($LASTEXITCODE -ne 0) {
        throw ("Falha no dotnet restore. Veja o log em '{0}'." -f $logFile)
    }

    if (Test-Path -LiteralPath $publishDir) {
        Write-Log "INFO" ("Limpando pasta de saida: {0}" -f $publishDir)
        Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction Stop
    }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    Write-Log "INFO" "Executando dotnet publish (single-file)..."
    Invoke-And-Log {
        & dotnet publish $projectPath `
            -c $Configuration `
            -r $RuntimeIdentifier `
            --self-contained true `
            --no-restore `
            -o $publishDir `
            /p:PublishSingleFile=true `
            /p:EnableCompressionInSingleFile=true `
            /p:IncludeNativeLibrariesForSelfExtract=true `
            /p:DebugType=None `
            /p:DebugSymbols=false `
            /p:GenerateRuntimeConfigurationFiles=false
    }
    if ($LASTEXITCODE -ne 0) {
        throw ("Falha no dotnet publish. Veja o log em '{0}'." -f $logFile)
    }

    # Garantir apenas o .exe na pasta de distribuição (remove .pdb, .json, .dll soltas, etc.)
    $exeName = ("{0}.exe" -f $assemblyName)
    Get-ChildItem -LiteralPath $publishDir -File -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -ne $exeName) {
            Write-Log "INFO" ("Removendo arquivo extra: {0}" -f $_.Name)
            Remove-Item -LiteralPath $_.FullName -Force -ErrorAction Stop
        }
    }

    if (Test-Path -LiteralPath $outputExe) {
        Write-Log "INFO" "Build concluido com sucesso."
        Write-Host ""
        Write-Host "EXE gerado em:"
        Write-Host $outputExe
    }
    else {
        Write-Log "AVISO" "O build terminou, mas o EXE nao foi localizado no caminho esperado."
        Write-Host $outputExe
    }

    exit 0
}
catch {
    Write-Log "ERRO" $_.Exception.Message
    exit 1
}
