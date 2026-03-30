param(
    [string]$ApiProjectPath = ".\MailMonitor.Api\MailMonitor.Api.csproj",
    [string]$WebProjectPath = ".\MailMonitor.Web",
    [ValidateSet("http", "https")]
    [string]$ApiLaunchProfile = "https",
    [string]$ApiHealthUrl = "http://localhost:5146/api/settings",
    [int]$ApiStartupTimeoutSeconds = 60,
    [switch]$SkipDotnetBuild,
    [switch]$SkipNpmInstall,
    [switch]$ForceRestart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-PathSafely {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Resolve-Path $Path).Path
}

function Get-RunningProcessByIdSafely {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId
    )

    return Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
}

function Stop-ProcessSafely {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId
    )

    $process = Get-RunningProcessByIdSafely -ProcessId $ProcessId
    if ($null -ne $process) {
        Stop-Process -Id $ProcessId -Force
    }
}

$repoRoot = Get-PathSafely -Path (Join-Path $PSScriptRoot "..")
$resolvedApiProjectPath = Get-PathSafely -Path (Join-Path $repoRoot $ApiProjectPath)
$resolvedWebProjectPath = Get-PathSafely -Path (Join-Path $repoRoot $WebProjectPath)

$logsDirectory = Join-Path $repoRoot "artifacts\dev-logs"
if (-not (Test-Path $logsDirectory)) {
    New-Item -ItemType Directory -Path $logsDirectory | Out-Null
}

$sessionFilePath = Join-Path $logsDirectory "dev-session.json"

if (Test-Path $sessionFilePath) {
    $existingSession = Get-Content -Path $sessionFilePath -Raw | ConvertFrom-Json
    $runningProcesses = @()

    if ($existingSession.ApiPid) {
        $apiProcess = Get-RunningProcessByIdSafely -ProcessId ([int]$existingSession.ApiPid)
        if ($null -ne $apiProcess) {
            $runningProcesses += $apiProcess
        }
    }

    if ($existingSession.WebPid) {
        $webProcess = Get-RunningProcessByIdSafely -ProcessId ([int]$existingSession.WebPid)
        if ($null -ne $webProcess) {
            $runningProcesses += $webProcess
        }
    }

    if ($runningProcesses.Count -gt 0) {
        if (-not $ForceRestart) {
            $runningIds = ($runningProcesses | Select-Object -ExpandProperty Id) -join ", "
            throw "Ya existe una sesion dev activa (PID: $runningIds). Usa -ForceRestart o ejecuta .\scripts\stop-dev.ps1"
        }

        foreach ($runningProcess in $runningProcesses) {
            Stop-ProcessSafely -ProcessId $runningProcess.Id
        }
    }
}

if (-not $SkipDotnetBuild) {
    Write-Host "Compilando API en Debug..."
    dotnet build $resolvedApiProjectPath -c Debug -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build fallo con codigo $LASTEXITCODE"
    }
}

if (-not $SkipNpmInstall) {
    $nodeModulesPath = Join-Path $resolvedWebProjectPath "node_modules"
    if (-not (Test-Path $nodeModulesPath)) {
        Write-Host "Instalando dependencias web (npm install)..."
        Push-Location $resolvedWebProjectPath
        try {
            & npm.cmd install
            if ($LASTEXITCODE -ne 0) {
                throw "npm install fallo con codigo $LASTEXITCODE"
            }
        } finally {
            Pop-Location
        }
    }
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$apiOutLogPath = Join-Path $logsDirectory "api-$timestamp.out.log"
$apiErrLogPath = Join-Path $logsDirectory "api-$timestamp.err.log"
$webOutLogPath = Join-Path $logsDirectory "web-$timestamp.out.log"
$webErrLogPath = Join-Path $logsDirectory "web-$timestamp.err.log"

Write-Host "Iniciando API..."
$apiProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--project", $resolvedApiProjectPath, "--launch-profile", $ApiLaunchProfile, "--no-build") `
    -WorkingDirectory $repoRoot `
    -RedirectStandardOutput $apiOutLogPath `
    -RedirectStandardError $apiErrLogPath `
    -PassThru

$apiIsHealthy = $false
$deadline = (Get-Date).AddSeconds($ApiStartupTimeoutSeconds)

while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 1

    if ($apiProcess.HasExited) {
        break
    }

    try {
        $healthResponse = Invoke-WebRequest -Uri $ApiHealthUrl -UseBasicParsing -TimeoutSec 3
        if ($healthResponse.StatusCode -ge 200 -and $healthResponse.StatusCode -lt 400) {
            $apiIsHealthy = $true
            break
        }
    } catch {
        # Espera activa hasta que la API este disponible.
    }
}

if (-not $apiIsHealthy) {
    Stop-ProcessSafely -ProcessId $apiProcess.Id

    $apiOutTail = if (Test-Path $apiOutLogPath) { Get-Content -Path $apiOutLogPath -Tail 30 | Out-String } else { "" }
    $apiErrTail = if (Test-Path $apiErrLogPath) { Get-Content -Path $apiErrLogPath -Tail 30 | Out-String } else { "" }

    throw "La API no inicio correctamente dentro de $ApiStartupTimeoutSeconds segundos. Revisa logs: $apiOutLogPath / $apiErrLogPath`n$apiOutTail`n$apiErrTail"
}

Write-Host "Iniciando Web..."
$webProcess = Start-Process -FilePath "npm.cmd" `
    -ArgumentList @("run", "dev") `
    -WorkingDirectory $resolvedWebProjectPath `
    -RedirectStandardOutput $webOutLogPath `
    -RedirectStandardError $webErrLogPath `
    -PassThru

Start-Sleep -Seconds 2
if ($webProcess.HasExited) {
    Stop-ProcessSafely -ProcessId $apiProcess.Id

    $webOutTail = if (Test-Path $webOutLogPath) { Get-Content -Path $webOutLogPath -Tail 30 | Out-String } else { "" }
    $webErrTail = if (Test-Path $webErrLogPath) { Get-Content -Path $webErrLogPath -Tail 30 | Out-String } else { "" }

    throw "El servidor Web finalizo al iniciar. Revisa logs: $webOutLogPath / $webErrLogPath`n$webOutTail`n$webErrTail"
}

$session = [PSCustomObject]@{
    StartedAt = (Get-Date).ToString("o")
    ApiPid = $apiProcess.Id
    WebPid = $webProcess.Id
    ApiOutLog = $apiOutLogPath
    ApiErrLog = $apiErrLogPath
    WebOutLog = $webOutLogPath
    WebErrLog = $webErrLogPath
    ApiHealthUrl = $ApiHealthUrl
}

$session | ConvertTo-Json | Set-Content -Path $sessionFilePath -Encoding UTF8

Write-Host ""
Write-Host "Sesion dev iniciada."
Write-Host "API PID: $($apiProcess.Id)"
Write-Host "WEB PID: $($webProcess.Id)"
Write-Host "Health API: $ApiHealthUrl"
Write-Host "Logs API: $apiOutLogPath / $apiErrLogPath"
Write-Host "Logs WEB: $webOutLogPath / $webErrLogPath"
Write-Host "Para detener: .\scripts\stop-dev.ps1"
