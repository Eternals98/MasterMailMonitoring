# Dia 4 - Runbook operativo (piloto)

Objetivo: operar MailMonitor con procedimiento claro de arranque, monitoreo, recuperacion y rollback.

## 1) Arranque

### API
1. Definir `ASPNETCORE_ENVIRONMENT`.
2. Definir variables `Persistence`, `Graph`, `Storage`.
3. Ejecutar:
   ```powershell
   dotnet run --project .\MailMonitor.Api\MailMonitor.Api.csproj
   ```
4. Verificar `GET /api/settings` y `GET /api/companies`.

### Worker
1. Definir `DOTNET_ENVIRONMENT`.
2. Confirmar acceso a DB SQLite y rutas de almacenamiento.
3. Iniciar como servicio (recomendado) o consola:
   ```powershell
   dotnet run --project .\MailMonitor.Worker\MailMonitor.Worker.csproj
   ```
4. Confirmar en logs al menos un ciclo:
   - `Mail processing cycle finished. Read: X, Processed: Y, Ignored: Z, Failed: W`.

## 2) Monitoreo operativo

### Salud funcional
- API:
  - `GET /api/settings`
  - `GET /api/email-statistics?from=<hoy>`
- Worker:
  - crecimiento de registros en `EmailStatistics`.
  - mensajes procesados con categoria `processingTag`.
  - presencia de archivos en rutas destino.

### Alertas recomendadas
- `Failed > 0` en ciclos consecutivos.
- picos de `Ignored` inesperados.
- errores de almacenamiento:
  - `STG-403` (permiso),
  - `STG-404` (ruta no disponible),
  - `STG-504` (fallo transitorio agotado).

## 3) Recuperacion (incidentes comunes)

### A) Falla de conectividad/ruta UNC
1. Validar acceso a share desde host del worker.
2. Corregir permisos de cuenta de servicio.
3. Ejecutar reinicio del servicio.
4. Confirmar recuperacion con nuevo ciclo exitoso.

### B) Credenciales de Graph invalidas
1. Validar `ClientId/TenantId/ClientSecret` vigentes.
2. Actualizar via endpoint `PUT /api/graph-settings` o variables de entorno.
3. Reiniciar worker.
4. Reprobar caso controlado.

### C) Bloqueo/corrupcion de SQLite
1. Detener API/worker.
2. Respaldar archivo `.db`.
3. Restaurar ultimo backup valido.
4. Reiniciar API/worker y validar endpoints.

## 4) Rollback

### Rollback de version
1. Detener servicio actual:
   ```powershell
   Stop-Service MailMonitor.Worker
   ```
2. Publicar/activar artefacto anterior conocido.
3. Reinstalar servicio (si cambia binario/ruta) con `install-worker-service.ps1 -ForceReinstall`.
4. Levantar servicio y validar ciclo.

### Rollback de configuracion
1. Restaurar variables de entorno previas.
2. Restaurar DB/backup de configuracion si aplica.
3. Reiniciar API y worker.
4. Ejecutar smoke test:
   - `GET /api/settings`
   - `GET /api/companies`
   - `GET /api/email-statistics`

## 5) Cierre de incidente
- Documentar causa raiz.
- Documentar hora de deteccion/mitigacion/cierre.
- Adjuntar evidencia de recuperacion (logs + queries API).
- Crear accion preventiva (hardening de alertas, permisos o configuracion).
