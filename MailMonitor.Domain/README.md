# MailMonitor.Domain

Base de reglas de negocio para el monitoreo de correos empresariales.

## Núcleo actual

- `Company`: configuración por empresa (correo, buzones, tipos de archivo, carpeta dinámica, `StartFrom`, tag de procesamiento y palabras clave).
- `Trigger`: regla de calendarización para el worker.
- `Setting` y `AppSetting`: parámetros globales (carpeta base, keywords, tag).
- `GraphSetting`: configuración de autenticación/scopes para Microsoft Graph.
- `EmailProcessStatistic`: registro de procesamiento por correo para reportería/auditoría.
- `StoredAttachmentInfo`: resultado de almacenamiento de adjuntos.

## Contratos de dominio

- `IConfigurationRepository`: persistencia de configuración global/empresas/triggers/Graph.
- `IEmailStatisticsRepository`: persistencia y consulta de estadísticas de procesamiento.

## Nota

El `Domain` no depende de infraestructura. La validación de reglas de negocio vive en las entidades y usa `Result/Error`.
