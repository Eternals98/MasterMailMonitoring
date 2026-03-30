# D5-P02 - Congelamiento de alcance de entrega

## Datos de control
- Fecha de congelamiento: 2026-03-29
- Fecha objetivo de entrega: 2026-04-01
- Version de referencia: Release candidata 2026-04-01
- Regla de cambio desde este punto: solo fixes P1/P2 y ajustes de estabilidad/documentacion.

## Alcance confirmado para esta entrega
1. API administrativa operativa para:
   - `GET/PUT /api/settings`
   - `GET/POST/PUT/DELETE /api/companies`
   - `GET/PUT /api/graph-settings`
   - `GET/POST/PUT/DELETE /api/triggers`
   - `GET /api/email-statistics`
   - `GET /api/reports/export`
   - `GET /api/health/graph`
2. Frontend operativo para:
   - configuracion global (`/settings`)
   - gestion de companies (`/companies`)
   - configuracion de Graph (`/graph-settings`)
   - monitoreo y exportacion (`/monitoring`)
3. Worker con ciclo productivo de lectura, filtrado, persistencia de adjuntos, tagging y registro estadistico.
4. Programacion por triggers (Quartz) con fallback de cron cuando no hay triggers validos.
5. Cobertura de pruebas unitarias e integracion existente en el repositorio y validacion manual documentada.
6. Documentacion operativa de Dia 4 (runbook, checklist, configuracion por ambiente y casos manuales).

## Fuera de alcance explicito (NO entra en esta entrega)
1. UI para CRUD de triggers (la gestion de triggers queda via API).
2. Autenticacion/autorizacion empresarial (SSO, RBAC por perfil, auditoria de permisos por usuario).
3. Integracion con gestor externo de secretos (Key Vault/Secret Manager) como unica fuente de credenciales.
4. Pipeline CI/CD completo con quality gates obligatorios y despliegue automatizado.
5. Observabilidad avanzada (dashboard dedicado, alertas proactivas y SLO/SLA instrumentados).
6. Reprocesamiento historico masivo y herramientas de backfill operado por lotes.
7. Hardening de alta disponibilidad (multi instancia activa-activa, cola distribuida, failover automatizado).
8. Suite E2E automatizada de frontend en navegador (Playwright/Cypress) integrada al pipeline.

## Impacto del congelamiento
- No se aceptan nuevas historias funcionales para esta release.
- Cualquier cambio fuera de lista entra al backlog post-release.
- Cambios permitidos hasta go-live:
  - correcciones P1/P2 con evidencia de prueba,
  - ajustes de configuracion/operacion,
  - mejoras de documentacion de cierre.

## Criterio de excepcion
Una excepcion de alcance solo se aprueba si cumple todo:
1. Riesgo de produccion alto si no se atiende (P1/P2).
2. Impacto acotado y sin alterar contratos publicos de API.
3. Evidencia de prueba asociada (unit/integration/manual segun aplique).
4. Aprobacion tecnica y funcional registrada en documento final de entrega.

## Backlog inmediato post-release
1. Pantalla de triggers en Web con CRUD y validacion cron.
2. Integracion de secretos en vault y rotacion de credenciales.
3. Automatizacion E2E web y ejecucion en CI.
4. Tablero de observabilidad con alertas operativas.
5. Endurecimiento HA y plan de escalamiento horizontal.
