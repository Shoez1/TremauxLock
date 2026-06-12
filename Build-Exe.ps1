param(
    [string]$Project = "LockerApp.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$projectRoot = $PSScriptRoot
$projectPath = Join-Path $projectRoot $Project
$logFile = Join-Path $projectRoot "build-exe.log"
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

[System.IO.File]::WriteAllText($logFile, "", $utf8NoBom)

function Write-Log {
    param(
        [string]$Level,
        [string]$Message
    )

    $line = "[{0}] {1}" -f $Level, $Message
    Write-Host $line
    [System.IO.File]::AppendAllText($logFile, $line + [Environment]::NewLine, $utf8NoBom)
}

function Invoke-And-Log {
    param(
        [scriptblock]$Action
    )

    & $Action 2>&1 | ForEach-Object {
        $text = $_.ToString()
        Write-Host $text
        [System.IO.File]::AppendAllText($logFile, $text + [Environment]::NewLine, $utf8NoBom)
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

    # Publica em uma pasta temporaria para nao apagar cofres salvos ao lado do exe.
    $publishDir = Join-Path $projectRoot ("dist\{0}" -f $RuntimeIdentifier)
    $tempPublishDir = Join-Path $projectRoot ("obj\publish\{0}" -f $RuntimeIdentifier)
    $outputExe = Join-Path $publishDir ("{0}.exe" -f $assemblyName)
    $tempOutputExe = Join-Path $tempPublishDir ("{0}.exe" -f $assemblyName)

    Write-Log "INFO" "Executando dotnet restore..."
    Invoke-And-Log { & dotnet restore $projectPath -r $RuntimeIdentifier }
    if ($LASTEXITCODE -ne 0) {
        throw ("Falha no dotnet restore. Veja o log em '{0}'." -f $logFile)
    }

    if (Test-Path -LiteralPath $tempPublishDir) {
        Write-Log "INFO" ("Limpando pasta temporaria de publish: {0}" -f $tempPublishDir)
        Remove-Item -LiteralPath $tempPublishDir -Recurse -Force -ErrorAction Stop
    }
    New-Item -ItemType Directory -Path $tempPublishDir -Force | Out-Null
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    Write-Log "INFO" "Executando dotnet publish (single-file)..."
    Invoke-And-Log {
        & dotnet publish $projectPath `
            -c $Configuration `
            -r $RuntimeIdentifier `
            --self-contained true `
            --no-restore `
            -o $tempPublishDir `
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

    if (-not (Test-Path -LiteralPath $tempOutputExe)) {
        throw ("O publish terminou, mas o EXE nao foi localizado em '{0}'." -f $tempOutputExe)
    }

    Write-Log "INFO" "Copiando EXE final sem apagar dados do cofre em dist..."
    Copy-Item -LiteralPath $tempOutputExe -Destination $outputExe -Force -ErrorAction Stop

    Write-Log "INFO" "Removendo pasta temporaria de publish..."
    Remove-Item -LiteralPath $tempPublishDir -Recurse -Force -ErrorAction Stop

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
