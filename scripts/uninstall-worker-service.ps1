param(
    [string]$ServiceName = "MailMonitor.Worker"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $service) {
    Write-Host "El servicio '$ServiceName' no existe."
    exit 0
}

if ($service.Status -ne "Stopped") {
    Write-Host "Deteniendo servicio '$ServiceName'..."
    Stop-Service -Name $ServiceName -Force
}

Write-Host "Eliminando servicio '$ServiceName'..."
& sc.exe delete $ServiceName | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "No se pudo eliminar el servicio '$ServiceName' (codigo $LASTEXITCODE)."
}

Write-Host "Servicio eliminado."
