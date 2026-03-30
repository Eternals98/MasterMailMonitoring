# ADR D3: Scheduling e Idempotencia Operativa

- Fecha: 2026-03-30
- Estado: Aprobado e implementado
- Alcance: `MailMonitor.Worker`, `MailMonitor.Infrastructure`, `MailMonitor.Api`

## Contexto
El motor anterior ejecutaba un loop fijo (`LoopDelaySeconds`) y reconsultaba correos desde `StartFrom` en cada ciclo. Eso dejaba huecos de confiabilidad:

- Riesgo de reprocesar correos ya tratados.
- Manejo insuficiente de errores transitorios en rutas UNC/red.
- Trazabilidad limitada para soporte (falta de correlación consistente).
- Seeds de secretos Graph en base de datos.

## Decisión 1: Scheduling por cron real con `Trigger`
Se adopta cron real usando Quartz con fuente en la tabla de `Trigger`:

- `MailMonitorJob` ejecuta el ciclo de monitoreo.
- `SchedulerService` carga triggers persistidos y los registra en Quartz.
- Si no hay triggers válidos, se aplica cron fallback controlado por configuración (`Scheduling:FallbackCronExpression`).

### Trade-offs
- Ventaja: ventanas de ejecución explícitas, auditables y controladas por negocio.
- Costo: mayor complejidad operativa frente al loop fijo.
- Mitigación: fallback cron y validación de expresiones inválidas con logs.

## Decisión 2: Idempotencia por `MessageId`
Se impide reproceso de mensajes ya procesados:

- Antes de descargar adjuntos, el filtro valida `HasProcessedMessage(messageId)`.
- Si ya fue procesado, se omite y se registra motivo de omisión.
- Si el correo ya tiene `ProcessingTag`, también se omite descarga (duplicado por tag).

### Trade-offs
- Ventaja: elimina reprocesos de adjuntos ya tratados.
- Costo: más consultas a estadísticas durante el ciclo.
- Mitigación: índice por `MessageId` y consulta puntual por mensaje.

## Decisión 3: Separación de responsabilidades en el motor
`Worker` pasa a ser orquestador y delega en servicios:

- Lectura: `IMailboxReader` (`GraphMailboxReader`)
- Filtro/idempotencia: `IMessageFilterService`
- Almacenamiento: `IAttachmentPersistenceService`
- Tagging: `IMessageTaggingService`
- Logging de estadísticas: `IProcessingLogService`

### Trade-offs
- Ventaja: mayor mantenibilidad, testabilidad y trazabilidad.
- Costo: más clases/DI.

## Decisión 4: Robustez de almacenamiento
`FileSystemAttachmentStorageService` se endurece con:

- Sanitización robusta de segmentos/rutas/nombres de archivo.
- Validación anti-path traversal contra base path.
- Reintentos exponenciales para errores transitorios de red/UNC.
- Clasificación de errores explícitos (permisos, ruta no disponible, path inválido) con códigos de log.

### Trade-offs
- Ventaja: menor tasa de fallos por intermitencia de red y rutas UNC.
- Costo: mayor latencia en fallos transitorios por reintentos.

## Decisión 5: Seguridad de secretos Graph por ambiente
Se elimina la semilla de secretos por defecto en DB y se prioriza configuración por ambiente:

- `GraphAuthFactory` combina settings persistidos con `Graph:*` de configuración (ej. variables de entorno).
- Migración limpia secretos legacy sembrados previamente en DB.

### Trade-offs
- Ventaja: evita secretos hardcodeados por defecto.
- Costo: requiere provisión de secretos por entorno para operar.

## Decisión 6: Observabilidad operativa
Se implementa:

- Correlación de logs por `messageId`, `company`, `mailbox`, `attachmentName` (scopes).
- Métricas por ciclo: leídos, procesados, ignorados, fallidos.
- Endpoint de diagnóstico `GET /api/health/graph` para verificar conectividad Graph.

## Consecuencias
- El motor queda orientado a operación real: cron gobernado por `Trigger`, idempotencia de mensajes y tolerancia a fallos de red/ruta.
- El diagnóstico en producción mejora por correlación, métricas por ciclo y healthcheck específico de Graph.
