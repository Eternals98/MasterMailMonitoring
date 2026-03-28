# Análisis del estado actual y plan de ejecución (fecha objetivo: 2026-04-01)

## 1) Contexto y aclaración de fechas

- Fecha actual de referencia: **sábado 2026-03-28**.
- La fecha indicada como "miércoles 31" tiene una inconsistencia de calendario:
  - **2026-03-31 cae martes**.
  - **2026-04-01 cae miércoles**.
- Para este plan, se asume entrega final el **miércoles 2026-04-01**.

## 2) Estado actual del proyecto (evaluación técnica)

### 2.1 Arquitectura encontrada

La solución contiene capas separadas y bien orientadas para un sistema híbrido (servicio + API + web):

- `MailMonitor.Worker` (proceso en background que monitorea correos y procesa adjuntos).
- `MailMonitor.Infrastructure` (acceso a Graph, persistencia SQLite, almacenamiento y reportes).
- `MailMonitor.Domain` (entidades y validaciones de negocio).
- `MailMonitor.Application` (abstracciones/interfaces).
- `MailMonitor.Api` (host web API, actualmente casi vacío funcionalmente).
- `MailMonitor.Web` (frontend Vite configurado, sin implementación funcional visible).

### 2.2 Funcionalidad ya implementada y usable

1. **Lectura de correos por MsGraph**
   - Cliente Graph con paginación y filtro por fecha (`receivedDateTime ge ...`).
   - Obtención de adjuntos tipo archivo.
   - Marcado por categorías/tags del mensaje.

2. **Ciclo de procesamiento en Worker**
   - Bucle continuo con intervalo configurable (`Processing:LoopDelaySeconds`).
   - Itera Companies y buzones, valida asunto por keywords globales.
   - Filtra adjuntos por extensión y keywords por Company.
   - Guarda adjuntos en filesystem y luego aplica tag.
   - Registra estadísticas por cada correo procesado/ignorado.

3. **Persistencia de configuración e histórico**
   - SQLite con creación automática de esquema y normalización de columnas.
   - Tablas de settings, companies, triggers, graph settings y estadísticas.
   - Servicio de reportes con lectura agregada y exportación Excel.

### 2.3 Brechas relevantes frente al objetivo solicitado

1. **API de administración no implementada realmente**
   - Los controladores `Companies`, `Settings`, `EmailStatistics` actuales son placeholders MVC (`Index()`), no endpoints REST para CRUD.

2. **Web app sin pantallas de operación**
   - Existe configuración base de Vite/paquete, pero no hay componentes/páginas para administrar Companies, intervalos, fechas, keywords, rutas o tags.

3. **Scheduling avanzado sin integrar**
   - Hay entidad `Trigger` y seed de cron, pero el Worker usa loop fijo por segundos.
   - La ejecución por cron por Company/Job no está conectada a una orquestación real.

4. **Idempotencia y duplicados**
   - El histórico evita duplicados por `MessageId` en estadísticas, pero no hay garantía fuerte de no reprocesar adjuntos ya descargados si cambia el estado o si el tag falla.

5. **Seguridad/operación**
   - Los secretos de Graph aparecen seed-eados en DB por defecto (aunque ofuscados), lo cual requiere endurecimiento para producción.
   - Falta estrategia formal de secretos (Windows Credential Manager/Key Vault/variables seguras).

6. **Pruebas automatizadas**
   - Unit tests e integration tests son plantillas vacías (sin cobertura real).

## 3) Nivel de avance estimado por módulo

- Núcleo de procesamiento (Worker + Graph + storage + logging estadístico): **65-75%**.
- Backend de administración (API para UI): **15-25%**.
- Frontend de control/monitoreo/reportes: **10-20%**.
- Calidad (tests, observabilidad, hardening, despliegue): **20-30%**.

> Conclusión: el proyecto tiene una base técnica sólida para el motor de procesamiento, pero **todavía no está listo para operación por usuarios de negocio** sin cerrar API, UI y estabilización.

## 4) Plan de ejecución para llegar a “listo” al 2026-04-01

## Día 1 — Sábado 2026-03-28 (Backend administrativo + contratos)

**Objetivo:** cerrar API REST consumible por frontend.

Tareas:
1. Definir DTOs y validaciones para:
   - Settings globales
   - Companies
   - Graph settings
   - Triggers
   - Filtros de estadísticas/reportes
2. Implementar endpoints CRUD reales en `MailMonitor.Api`:
   - `GET/PUT /api/settings`
   - `GET/POST/PUT/DELETE /api/companies`
   - `GET/PUT /api/graph-settings`
   - `GET/POST/PUT/DELETE /api/triggers`
   - `GET /api/email-statistics` + filtros
   - `GET /api/reports/export` (Excel)
3. Documentar contratos en Swagger con ejemplos.
4. Pruebas API básicas de humo (colección HTTP/postman).

## Día 2 — Domingo 2026-03-29 (Frontend funcional mínimo)

**Objetivo:** panel web usable para configuración y monitoreo.

Tareas:
1. Crear estructura de frontend (si aplica React/Vite): layout, routing, manejo de estado.
2. Pantallas:
   - Companies (CRUD + mailbox ids + keywords + tipos + rutas + tag)
   - Settings globales (base folder, keywords globales, loop/intervalo)
   - Graph settings
   - Estadísticas (tabla, filtros por fecha/company/procesado)
   - Reportes (botón de exportación Excel)
3. Validaciones de formularios y mensajes de error/éxito.
4. Integración real con API (`httpClient.ts`).

## Día 3 — Lunes 2026-03-30 (Motor de ejecución + robustez)

**Objetivo:** comportamiento productivo y confiable.

Tareas:
1. Integrar `Trigger` real (cron) o justificar mantener loop + parametrización por Company.
2. Evitar reprocesamiento:
   - Regla de idempotencia por `MessageId` + estado/tag ya aplicado.
3. Robustecer almacenamiento en rutas de red UNC:
   - reintentos, timeouts, manejo de permisos, path sanitization.
4. Mejorar trazabilidad:
   - correlación por messageId/company/mailbox.
5. Revisar estrategia de secretos y quitar seeds sensibles por defecto.

## Día 4 — Martes 2026-03-31 (QA integral + despliegue piloto)

**Objetivo:** validar extremo a extremo y empaquetar.

Tareas:
1. Tests unitarios críticos:
   - filtros de asunto
   - filtros de adjuntos
   - validaciones Company/GraphSetting
2. Tests de integración:
   - API + SQLite
   - ejecución Worker sobre datos de prueba
3. Pruebas E2E manuales:
   - correo válido con adjunto permitido
   - correo no válido por asunto
   - correo válido sin adjuntos
   - fallo de ruta de red
4. Preparar despliegue:
   - servicio windows (sc create / NSSM / instalador)
   - variables y configuración por ambiente
   - checklist de operación y soporte

## Día 5 — Miércoles 2026-04-01 (buffer final)

**Objetivo:** contingencia + cierre.

Tareas:
1. Correcciones finales de bugs críticos.
2. Ensayo de operación (runbook).
3. Aprobación funcional y técnica.

## 5) Definición de “Listo para producción” (DoD)

Se considera listo cuando se cumpla todo:
1. CRUD completo desde UI para Companies/Settings/Graph/Triggers.
2. Worker ejecutando en ambiente objetivo con al menos 2 Companies activas.
3. Descarga en ruta local y ruta de red validada.
4. Tag por Company aplicado exitosamente en Graph.
5. Histórico y exportación Excel operativos.
6. Logs con trazabilidad por MessageId.
7. Suite mínima de pruebas pasando en CI/local.
8. Manual operativo + checklist de recuperación.

## 6) Riesgos y mitigaciones

1. **Permisos sobre buzones y folders en Graph**
   - Mitigación: prueba temprana con credenciales reales el Día 1.
2. **Permisos de escritura en carpetas de red**
   - Mitigación: prueba técnica con cuenta de servicio desde Día 2.
3. **Latencia o throttling de Graph**
   - Mitigación: backoff/retry y límites por lote.
4. **Cambios de alcance de UI**
   - Mitigación: bloquear MVP de pantallas en Día 2.

## 7) Prompts sugeridos por tareas (específicos e individuales)

> Puedes usar estos prompts directamente en sesiones separadas para ejecutar cada tarea.

### Prompt 1 — Implementar API de configuración global
"Implementa en `MailMonitor.Api` endpoints REST reales para `GET/PUT /api/settings` usando `IConfigurationService`. Incluye DTOs, validación, códigos HTTP correctos, ejemplos Swagger, y pruebas de humo en `MailMonitor.Api.http`. No uses controladores MVC de vista."

### Prompt 2 — Implementar API CRUD de Companies
"Implementa `GET/POST/PUT/DELETE /api/companies` en `MailMonitor.Api` con DTOs y validaciones (mail obligatorio, storage/report folder obligatorios, startFrom fecha válida, listas normalizadas sin duplicados). Debe persistir vía `IConfigurationService`. Agrega filtros por nombre/correo en GET list."

### Prompt 3 — Implementar API de Graph Settings segura
"Implementa `GET/PUT /api/graph-settings` en `MailMonitor.Api`, evitando devolver `ClientSecret` completo en responses (enmascarado parcial). Agrega validación de scopes JSON y endpoint opcional de prueba de conexión a Graph."

### Prompt 4 — Implementar API CRUD de Triggers
"Implementa `GET/POST/PUT/DELETE /api/triggers` en `MailMonitor.Api` con validación de expresión cron y control de errores. Añade ejemplos en Swagger y pruebas de solicitud en archivo `.http`."

### Prompt 5 — Implementar API de estadísticas y exportación
"Implementa `GET /api/email-statistics` con filtros (`from`, `to`, `company`, `processed`) y paginación. Implementa `GET /api/reports/export?from=&to=&company=` que genere Excel usando `IEmailStatisticsExporter` y retorne archivo descargable."

### Prompt 6 — Construir frontend de Companies
"Crea en `MailMonitor.Web` la pantalla `Companies` con tabla + formulario modal para CRUD. Campos: nombre, correo, mailboxIds (multivalue), tipos de archivo, attachment keywords, storage folder, report output folder, processing tag, startFrom. Conecta a `/api/companies` y muestra validaciones inline."

### Prompt 7 — Construir frontend de Settings globales
"Crea pantalla `Settings` para editar `BaseStorageFolder`, `MailSubjectKeywords`, `ProcessingTag`, `LoopDelaySeconds`. Conecta a `/api/settings` y muestra feedback de guardado y errores de API."

### Prompt 8 — Construir frontend de Graph Settings
"Crea pantalla `Graph Settings` con formulario para Instance, ClientId, TenantId, ClientSecret, Scopes. Maneja secreto enmascarado y opción de actualizar sin exponer valor actual."

### Prompt 9 — Construir frontend de monitoreo/reportes
"Crea pantalla `Monitoring` con tabla de estadísticas, filtros por fecha/company/estado, botón de exportación Excel, y resumen KPI (procesados/ignorados por día). Consume endpoints nuevos de estadísticas/reportes."

### Prompt 10 — Idempotencia en Worker
"Refactoriza `Worker` para evitar reprocesar correos ya tratados usando `MessageId` + verificación de tag/categoría. Si un correo ya tiene el tag de la Company, omitir descarga y registrar estadística como ignorado por duplicado."

### Prompt 11 — Robustez de almacenamiento en red
"Mejora `FileSystemAttachmentStorageService` con reintentos exponenciales, manejo explícito de errores de permisos/red, y logs estructurados por company/messageId/fileName. Añade tests unitarios para rutas inválidas y colisiones de nombre."

### Prompt 12 — Pruebas unitarias de dominio y filtros
"Escribe pruebas unitarias para validaciones de `Company` y `GraphSetting`, parsing de keywords, filtro de adjuntos por extensión/palabras, y sanitización de asunto. Reemplaza tests plantilla vacíos."

### Prompt 13 — Pruebas de integración API + SQLite
"Agrega pruebas de integración para endpoints críticos (`settings`, `companies`, `email-statistics`) usando una base SQLite temporal por prueba y validando persistencia real."

### Prompt 14 — Empaquetado servicio Windows + runbook
"Documenta e implementa scripts de despliegue para registrar `MailMonitor.Worker` como Windows Service, configuración por ambiente, rotación de logs y procedimiento de rollback. Genera `docs/Runbook_Operacion.md`."


## 7.1) Prompts prácticos y pequeños para ejecutar SOLO el Día 1

> Estos prompts están pensados para correr en sesiones separadas de Codex, con alcance reducido, entregable claro y fácil de validar.

### D1-P01 — Crear carpeta de contratos API
"En `MailMonitor.Api`, crea la estructura de carpetas `Contracts/Settings`, `Contracts/Companies`, `Contracts/GraphSettings`, `Contracts/Triggers`, `Contracts/Statistics`. No implementes lógica todavía; solo estructura y archivos vacíos con comentarios TODO."

### D1-P02 — DTO de lectura de Settings
"Crea `SettingsResponse` en `MailMonitor.Api/Contracts/Settings` con campos: `baseStorageFolder`, `mailSubjectKeywords`, `processingTag`. Ajusta serialización para camelCase."

### D1-P03 — DTO de actualización de Settings
"Crea `UpdateSettingsRequest` con validaciones mínimas (`baseStorageFolder` requerido). No implementes endpoint aún; solo DTO + data annotations."

### D1-P04 — Endpoint GET /api/settings
"Implementa `GET /api/settings` en un controlador API real (no MVC view), consumiendo `IConfigurationService.GetSettingsAsync()`, mapeando a `SettingsResponse`, devolviendo `200 OK`."

### D1-P05 — Endpoint PUT /api/settings
"Implementa `PUT /api/settings` que reciba `UpdateSettingsRequest`, valide modelo, persista con `IConfigurationService.UpdateSettingsAsync`, y responda `204 NoContent` o `400 BadRequest` con errores de validación."

### D1-P06 — DTOs base de Company
"Crea `CompanyListItemResponse`, `CompanyDetailResponse` y `UpsertCompanyRequest` en `Contracts/Companies` con campos necesarios del dominio actual. Incluye listas `mailBox`, `fileTypes`, `attachmentKeywords`."

### D1-P07 — GET /api/companies (lista)
"Implementa `GET /api/companies` con respuesta de lista. Añade filtros query opcionales `name` y `mail` (contains, case-insensitive)."

### D1-P08 — GET /api/companies/{id}
"Implementa `GET /api/companies/{id}` con `404` si no existe y `200` con `CompanyDetailResponse` si existe."

### D1-P09 — POST /api/companies
"Implementa `POST /api/companies` usando `UpsertCompanyRequest`, mapeo a `Company`, persistencia con `AddOrUpdateCompanyAsync` y respuesta `201 Created` con `Location`."

### D1-P10 — PUT /api/companies/{id}
"Implementa `PUT /api/companies/{id}` con validación de consistencia de `id` ruta vs body, actualización y respuesta `204` / `404`."

### D1-P11 — DELETE /api/companies/{id}
"Implementa `DELETE /api/companies/{id}` usando `DeleteCompanyAsync`, retornando `204` si elimina y `404` si no existe."

### D1-P12 — DTOs de GraphSettings
"Crea `GraphSettingsResponse` (con `clientSecretMasked`) y `UpdateGraphSettingsRequest`. No expongas `clientSecret` completo en respuestas."

### D1-P13 — GET /api/graph-settings
"Implementa `GET /api/graph-settings` con enmascaramiento de secreto (solo últimos 4 caracteres visibles)."

### D1-P14 — PUT /api/graph-settings
"Implementa `PUT /api/graph-settings` con validación de JSON de scopes, persistencia y respuesta `204`/`400`."

### D1-P15 — DTOs de Trigger
"Crea `TriggerResponse` y `UpsertTriggerRequest` en `Contracts/Triggers` con validación de `name` y `cronExpression` requeridos."

### D1-P16 — GET /api/triggers
"Implementa `GET /api/triggers` retornando todos los triggers configurados."

### D1-P17 — POST /api/triggers
"Implementa `POST /api/triggers` con validación de cron expression (usa librería existente o validación básica) y `201 Created`."

### D1-P18 — PUT /api/triggers/{id}
"Implementa `PUT /api/triggers/{id}` con validación de id y cron expression, respuesta `204`/`404`/`400`."

### D1-P19 — DELETE /api/triggers/{id}
"Implementa `DELETE /api/triggers/{id}` con respuesta `204`/`404`."

### D1-P20 — Endpoint de estadísticas (MVP)
"Implementa `GET /api/email-statistics` versión mínima con filtros opcionales `from`, `to`, `company`, `processed`, sin paginación aún; devuelve lista ordenada desc por fecha."

### D1-P21 — Export Excel (MVP)
"Implementa `GET /api/reports/export` que recupere estadísticas, genere archivo temporal con `IEmailStatisticsExporter` y lo devuelva como `FileResult`."

### D1-P22 — Swagger: ejemplos y descripciones
"Agrega documentación Swagger para todos los endpoints del Día 1: summary, códigos de respuesta, y ejemplos request/response."

### D1-P23 — Archivo de pruebas HTTP
"Actualiza `MailMonitor.Api.http` con ejemplos ejecutables para cada endpoint del Día 1 (GET/POST/PUT/DELETE), incluyendo casos de error 400 y 404."

### D1-P24 — Checklist de cierre Día 1
"Crea `docs/checklists/Dia1_API_Checklist.md` con checklist de verificación funcional endpoint por endpoint (status code esperado, payload ejemplo, evidencia)."


## 8) Recomendación final

Dado el estado actual, la estrategia óptima es:
1) **cerrar API primero**, 2) **levantar UI mínima funcional**, 3) **endurecer Worker**, 4) **QA + despliegue controlado**.

Con disciplina por lotes y congelando alcance no crítico, la meta del **miércoles 2026-04-01** es alcanzable.
