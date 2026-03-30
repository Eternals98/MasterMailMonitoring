# Prompts completos D1 a D5 (listos para correr)

> Objetivo: ejecutar el plan completo hasta la entrega del **2026-04-01**.
> Recomendación: correr 1 prompt por sesión de Codex para mantener cambios pequeños y revisables.

## Contexto general del proyecto

Servicio de Windows + aplicativo web para monitorear buzones vía Microsoft Graph, filtrar correos por asunto/keywords, descargar adjuntos a rutas locales o de red, etiquetar correos por Company y registrar histórico/reportes.

Estos prompts están organizados para cerrar primero operación básica (API + Web), luego robustez del motor y finalmente QA/cierre de entrega.

## Índice de numeración (sin saltos)

Para evitar confusión al leer el documento, esta es la secuencia completa por día:

- **D1:** `D1-P01` ... `D1-P24` (24 prompts).
- **D2:** `D2-P01` ... `D2-P24` (24 prompts).
- **D3:** `D3-P01` ... `D3-P12` (12 prompts).
- **D4:** `D4-P01` ... `D4-P12` (12 prompts).
- **D5:** `D5-P01` ... `D5-P08` (8 prompts).

> Si en la vista previa parece que “salta” (por ejemplo de `P09` a `P18`), normalmente es por recorte/plegado visual del visor, no por ausencia real en el archivo.

## Día 1 — Backend API (D1)

**Contexto general (por qué en este proyecto):**
El proyecto necesita una API administrativa real para que la web pueda configurar Companies, credenciales/ajustes Graph, triggers, filtros de búsqueda y reportes. Sin D1, el Worker queda aislado y no hay operación controlada por negocio.

### D1-P01
"Crea en `MailMonitor.Api` las carpetas `Contracts/Settings`, `Contracts/Companies`, `Contracts/GraphSettings`, `Contracts/Triggers`, `Contracts/Statistics` con archivos placeholder TODO. No implementes lógica todavía."

### D1-P02
"Crea `SettingsResponse` en `Contracts/Settings` con `baseStorageFolder`, `mailSubjectKeywords`, `processingTag` en camelCase."

### D1-P03
"Crea `UpdateSettingsRequest` en `Contracts/Settings` con validación (`baseStorageFolder` requerido)."

### D1-P04
"Implementa `GET /api/settings` en controlador API real, usando `IConfigurationService.GetSettingsAsync()` y devolviendo `SettingsResponse` con `200 OK`."

### D1-P05
"Implementa `PUT /api/settings` con `UpdateSettingsRequest`, validación de modelo y persistencia con `UpdateSettingsAsync`; devuelve `204`/`400`."

### D1-P06
"Crea DTOs `CompanyListItemResponse`, `CompanyDetailResponse`, `UpsertCompanyRequest` en `Contracts/Companies`."

### D1-P07
"Implementa `GET /api/companies` con filtros opcionales `name` y `mail` (contains, case-insensitive)."

### D1-P08
"Implementa `GET /api/companies/{id}` con `200`/`404`."

### D1-P09
"Implementa `POST /api/companies` con mapeo a dominio y `201 Created` + `Location`."

### D1-P10
"Implementa `PUT /api/companies/{id}` validando consistencia de ID ruta/body y responde `204`/`404`/`400`."

### D1-P11
"Implementa `DELETE /api/companies/{id}` usando `DeleteCompanyAsync`, retorna `204` o `404`."

### D1-P12
"Crea `GraphSettingsResponse` (incluyendo `clientSecretMasked`) y `UpdateGraphSettingsRequest`."

### D1-P13
"Implementa `GET /api/graph-settings` enmascarando secreto (solo últimos 4 caracteres visibles)."

### D1-P14
"Implementa `PUT /api/graph-settings` con validación de scopes JSON y respuesta `204`/`400`."

### D1-P15
"Crea DTOs `TriggerResponse` y `UpsertTriggerRequest` con validaciones requeridas de `name` y `cronExpression`."

### D1-P16
"Implementa `GET /api/triggers` devolviendo todos los triggers."

### D1-P17
"Implementa `POST /api/triggers` con validación de cron y `201 Created`."

### D1-P18
"Implementa `PUT /api/triggers/{id}` con validación de id + cron y respuestas `204`/`404`/`400`."

### D1-P19
"Implementa `DELETE /api/triggers/{id}` con respuestas `204`/`404`."

### D1-P20
"Implementa `GET /api/email-statistics` MVP con filtros `from`, `to`, `company`, `processed` y orden desc por fecha."

### D1-P21
"Implementa `GET /api/reports/export` MVP usando `IEmailStatisticsExporter` y devuelve archivo Excel descargable."

### D1-P22
"Documenta endpoints D1 en Swagger con summary, responses y ejemplos."

### D1-P23
"Actualiza `MailMonitor.Api.http` con ejemplos de éxito/error para todos los endpoints D1."

### D1-P24
"Crea `docs/checklists/Dia1_API_Checklist.md` con checklist de validación endpoint por endpoint."

---

## Día 2 — Frontend web mínimo funcional (D2)

**Contexto general (por qué en este proyecto):**
La solución requiere una interfaz para operación diaria (altas/bajas/cambios), monitoreo y exportación. D2 traduce la API a flujos de usuario reales para que el equipo no dependa de tocar base de datos o archivos manualmente.

### D2-P01
"En `MailMonitor.Web` crea estructura `src/main`, `src/App`, `src/pages`, `src/components`, `src/services`, `src/types` y deja app arrancando."

### D2-P02
"Implementa router con rutas `/companies`, `/settings`, `/graph-settings`, `/monitoring` y home con navegación."

### D2-P03
"Crea `httpClient` central con `VITE_API_BASE_URL`, timeout y manejo base de errores."

### D2-P04
"Define tipos TS para `Setting`, `Company`, `GraphSetting`, `Trigger`, `EmailStatistic`."

### D2-P05
"Implementa `settingsService` (`getSettings`, `updateSettings`)."

### D2-P06
"Crea vista `/settings` de lectura con estados loading/error/empty."

### D2-P07
"Agrega formulario de edición en `/settings` con feedback de guardado."

### D2-P08
"Implementa `companiesService` (`list/getById/create/update/remove`) con filtros."

### D2-P09
"Crea tabla de companies con acciones editar/eliminar."

### D2-P10
"Crea formulario create/edit de company con campos multivalor."

### D2-P11
"Agrega validaciones frontend de company (correo/rutas/fecha/listas sin duplicados)."

### D2-P12
"Implementa confirmación de borrado y refresco de lista."

### D2-P13
"Implementa `graphSettingsService` (`get/update`) soportando secreto enmascarado."

### D2-P14
"Crea pantalla `/graph-settings` con ayuda para scopes y actualización segura."

### D2-P15
"Implementa `statisticsService` (`list` y `exportExcel`)."

### D2-P16
"Crea filtros en `/monitoring` por fecha, company y estado."

### D2-P17
"Crea tabla de estadísticas + KPI (procesados/ignorados)."

### D2-P18
"Agrega botón Exportar Excel con descarga de archivo."

### D2-P19
"Implementa layout base con menú y título por página."

### D2-P20
"Crea componentes reutilizables `Loading`, `ErrorMessage`, `EmptyState`, `Toast`."

### D2-P21
"Centraliza manejo de errores API (400/404/500/network)."

### D2-P22
"Agrega `.env.example` con `VITE_API_BASE_URL` y mini guía de uso."

### D2-P23
"Crea `docs/checklists/Dia2_Web_Checklist.md` con pruebas manuales por pantalla."

### D2-P24
"Aplica mejoras UX mínimas: disable durante submit, success message, focus en primer error."

---

## Día 3 — Motor y robustez (D3)

**Contexto general (por qué en este proyecto):**
Aquí se asegura confiabilidad operativa: evitar reprocesos, tolerar errores de red/rutas UNC, mejorar trazabilidad y endurecer seguridad de secretos. D3 reduce incidentes en producción y facilita soporte.

### D3-P01
"Define estrategia de scheduling: integrar cron real con `Trigger` o justificar técnicamente loop fijo por Company. Implementa la opción elegida."

### D3-P02
"Implementa idempotencia por `MessageId`: no reprocesar correos ya tratados y registrar motivo de omisión."

### D3-P03
"Si el correo ya tiene el `ProcessingTag` de Company, omite descarga y registra estadística de duplicado."

### D3-P04
"Refactoriza `Worker` para separar responsabilidades: lectura, filtro, almacenamiento, tagging, logging."

### D3-P05
"Mejora `FileSystemAttachmentStorageService` con reintentos exponenciales para errores transitorios de red/UNC."

### D3-P06
"Agrega manejo explícito de errores de permisos/ruta no disponible con códigos y mensajes de log claros."

### D3-P07
"Añade correlación de logs por `messageId`, `company`, `mailbox`, `attachmentName`."

### D3-P08
"Asegura sanitización robusta de rutas/nombres para prevenir path traversal y caracteres inválidos."

### D3-P09
"Revisa seeds de secretos Graph y migra a configuración segura por ambiente (sin secretos por defecto en DB)."

### D3-P10
"Implementa endpoint/healthcheck de verificación de conectividad Graph para diagnóstico operativo."

### D3-P11
"Agrega métricas mínimas: correos leídos, procesados, ignorados, fallidos por ciclo."

### D3-P12
"Crea `docs/ADR_Scheduling_Idempotencia.md` con decisiones técnicas y trade-offs del Día 3."

---

## Día 4 — QA + despliegue piloto (D4)

**Contexto general (por qué en este proyecto):**
El objetivo es validar extremo a extremo antes de salida piloto: cobertura mínima de pruebas, verificación de integración real y documentación de despliegue/operación para reducir riesgo de fallas en ambientes reales.

### D4-P01
"Crea pruebas unitarias para validaciones de `Company` y `GraphSetting`."

### D4-P02
"Crea pruebas unitarias para filtro de asunto y parsing de keywords."

### D4-P03
"Crea pruebas unitarias para filtro de adjuntos por extensión y keywords."

### D4-P04
"Crea pruebas unitarias de sanitización de asunto y nombres de archivo."

### D4-P05
"Agrega integración API + SQLite para `/api/settings` (GET/PUT)."

### D4-P06
"Agrega integración API + SQLite para `/api/companies` (CRUD)."

### D4-P07
"Agrega integración API + SQLite para `/api/email-statistics` y filtros."

### D4-P08
"Define y documenta casos E2E manuales (válido/no válido/sin adjuntos/fallo ruta red)."

### D4-P09
"Prepara script de instalación de `MailMonitor.Worker` como Windows Service (o instrucciones reproducibles)."

### D4-P10
"Documenta configuración por ambiente (`Development`, `QA`, `Production`) y variables requeridas."

### D4-P11
"Genera runbook operativo con arranque, monitoreo, recuperación y rollback."

### D4-P12
"Crea checklist de salida piloto `docs/checklists/Dia4_QA_Deploy_Checklist.md`."

---

## Día 5 — Cierre y contingencia (D5)

**Contexto general (por qué en este proyecto):**
D5 consolida calidad y gobernanza de entrega: cierre de críticos, validación de DoD, evidencias finales y plan de rollback. Esto evita liberar algo incompleto o sin capacidad de recuperación.

### D5-P01
"Ejecuta triage final de bugs abiertos, clasifica por severidad y corrige los críticos."

### D5-P02
"Congela alcance: documenta explícitamente qué queda fuera de esta entrega."

### D5-P03
"Corre suite completa (unit/integration/manual) y publica resultados en `docs/Resultados_QA_Final.md`."

### D5-P04
"Realiza ensayo de operación de punta a punta y documenta tiempos/resultados."

### D5-P05
"Genera checklist de go-live y criterio de rollback con responsables."

### D5-P06
"Actualiza changelog y notas de versión con cambios funcionales y técnicos."

### D5-P07
"Valida DoD completo (8 puntos) y marca cumplimiento con evidencia."

### D5-P08
"Publica documento final de entrega `docs/Entrega_Final_2026-04-01.md` con riesgos remanentes y próximos pasos."
## Estado de validacion (2026-03-29)

### D2 comprobado
- [x] D2-P01
- [x] D2-P02
- [x] D2-P03
- [x] D2-P04
- [x] D2-P05
- [x] D2-P06
- [x] D2-P07
- [x] D2-P08
- [x] D2-P09
- [x] D2-P10
- [x] D2-P11
- [x] D2-P12
- [x] D2-P13
- [x] D2-P14
- [x] D2-P15
- [x] D2-P16
- [x] D2-P17
- [x] D2-P18
- [x] D2-P19
- [x] D2-P20
- [x] D2-P21
- [x] D2-P22
- [x] D2-P23
- [x] D2-P24

Validacion tecnica ejecutada:
- [x] `npm.cmd run build` exitoso en `MailMonitor.Web`.