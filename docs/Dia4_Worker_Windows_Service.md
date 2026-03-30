# Dia 4 - Instalacion de MailMonitor.Worker como Windows Service

Este procedimiento deja una instalacion reproducible para entorno piloto/QA usando PowerShell.

## Requisitos
- Windows Server/Windows 10+ con permisos de administrador.
- .NET 8 Runtime instalado.
- Acceso al repositorio en servidor destino.
- Cuenta de servicio con permisos a:
  - base SQLite (`Persistence:ConfigurationDbPath`),
  - carpetas locales/UNC de almacenamiento y reportes,
  - red hacia Microsoft Graph.

## Script de instalacion
Archivo: `scripts/install-worker-service.ps1`

Ejemplo de ejecucion desde raiz del repo:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-worker-service.ps1 `
  -ServiceName "MailMonitor.Worker.QA" `
  -Configuration "Release" `
  -PublishOutputPath ".\artifacts\worker\publish" `
  -StartService
```

Opciones clave:
- `-ForceReinstall`: elimina e instala nuevamente si el servicio ya existe.
- `-StartService`: inicia el servicio al final.

## Desinstalacion / rollback rapido
Archivo: `scripts/uninstall-worker-service.ps1`

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall-worker-service.ps1 `
  -ServiceName "MailMonitor.Worker.QA"
```

## Verificacion post-instalacion
1. `Get-Service MailMonitor.Worker.QA` debe quedar en `Running`.
2. Revisar Event Viewer + logs de aplicacion.
3. Confirmar que el worker ejecuta ciclos y persiste estadisticas.
4. Confirmar aplicacion de categoria (`processingTag`) en mensajes procesados.
