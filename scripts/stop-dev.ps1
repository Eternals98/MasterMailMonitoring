param(
    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Stop-ProcessSafely {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId
    )

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -ne $process) {
        & taskkill.exe /PID $ProcessId /T /F | Out-Null
        return $LASTEXITCODE -eq 0
    }

    return $false
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$sessionFilePath = Join-Path $repoRoot "artifacts\dev-logs\dev-session.json"

if (-not (Test-Path $sessionFilePath)) {
    if (-not $Quiet) {
        Write-Host "No existe sesion dev activa (archivo no encontrado)."
    }

    exit 0
}

$session = Get-Content -Path $sessionFilePath -Raw | ConvertFrom-Json
$stopped = @()
$missing = @()

if ($session.ApiPid) {
    if (Stop-ProcessSafely -ProcessId ([int]$session.ApiPid)) {
        $stopped += "API:$($session.ApiPid)"
    } else {
        $missing += "API:$($session.ApiPid)"
    }
}

if ($session.WebPid) {
    if (Stop-ProcessSafely -ProcessId ([int]$session.WebPid)) {
        $stopped += "WEB:$($session.WebPid)"
    } else {
        $missing += "WEB:$($session.WebPid)"
    }
}

Remove-Item -Path $sessionFilePath -Force

if (-not $Quiet) {
    if ($stopped.Count -gt 0) {
        Write-Host "Procesos detenidos: $($stopped -join ', ')"
    }

    if ($missing.Count -gt 0) {
        Write-Host "Procesos no encontrados: $($missing -join ', ')"
    }

    if ($stopped.Count -eq 0 -and $missing.Count -eq 0) {
        Write-Host "No habia procesos para detener."
    }
}
