param(
    [string]$ServiceName = "MailMonitor.Worker",
    [string]$WorkerProjectPath = ".\MailMonitor.Worker\MailMonitor.Worker.csproj",
    [string]$PublishOutputPath = ".\artifacts\worker\publish",
    [string]$Configuration = "Release",
    [switch]$StartService,
    [switch]$ForceReinstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Sc {
    param([string[]]$Arguments)

    & sc.exe @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe fallo con codigo $LASTEXITCODE. Args: $($Arguments -join ' ')"
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedProjectPath = Resolve-Path (Join-Path $repoRoot $WorkerProjectPath)
$resolvedPublishPath = Join-Path $repoRoot $PublishOutputPath

Write-Host "Publicando worker..."
dotnet publish $resolvedProjectPath -c $Configuration -o $resolvedPublishPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish fallo con codigo $LASTEXITCODE"
}

$workerExe = Join-Path $resolvedPublishPath "MailMonitor.Worker.exe"
if (-not (Test-Path $workerExe)) {
    throw "No se encontro el ejecutable publicado: $workerExe"
}

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -ne $existingService) {
    if (-not $ForceReinstall) {
        throw "El servicio '$ServiceName' ya existe. Usa -ForceReinstall para reinstalar."
    }

    Write-Host "Reinstalando servicio existente '$ServiceName'..."
    if ($existingService.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName -Force
    }

    Invoke-Sc -Arguments @("delete", $ServiceName)
    Start-Sleep -Seconds 2
}

$binPath = "`"$workerExe`" --contentRoot `"$resolvedPublishPath`""

Write-Host "Creando servicio '$ServiceName'..."
Invoke-Sc -Arguments @("create", $ServiceName, "binPath=", $binPath, "start=", "auto", "DisplayName=", $ServiceName)
Invoke-Sc -Arguments @("description", $ServiceName, "MailMonitor worker service for mailbox processing.")
Invoke-Sc -Arguments @("failure", $ServiceName, "reset=", "86400", "actions=", "restart/5000/restart/5000/restart/5000")

if ($StartService) {
    Write-Host "Iniciando servicio '$ServiceName'..."
    Start-Service -Name $ServiceName
}

Write-Host "Instalacion completada."
Write-Host "Servicio: $ServiceName"
Write-Host "Ejecutable: $workerExe"
