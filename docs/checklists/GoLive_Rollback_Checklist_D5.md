# Checklist Go-Live y Rollback (D5-P05)

## Datos
- Fecha objetivo de go-live: 2026-04-01
- Version objetivo: 2026.04.01

## Responsables
- Tech Lead: Responsable de aprobacion tecnica final.
- QA Lead: Responsable de evidencias de pruebas y decision de calidad.
- Ops Lead: Responsable de despliegue y monitoreo inicial.
- Product Owner: Responsable de aprobacion funcional y comunicacion.

## 1) Checklist pre go-live
- [ ] Tech Lead: Codigo en rama de release sin P1 abiertos.
- [ ] QA Lead: `docs/Resultados_QA_Final.md` aprobado.
- [ ] Ops Lead: Variables de ambiente cargadas en entorno objetivo.
- [ ] Ops Lead: Ruta de almacenamiento local/UNC validada con cuenta de servicio.
- [ ] Ops Lead: Servicio `MailMonitor.Worker` instalado y en `Running`.
- [ ] Tech Lead: Endpoints API de salud verificados (`/api/health/graph` y smoke endpoints).
- [ ] QA Lead: Casos manuales Graph/UNC ejecutados en ambiente objetivo.
- [ ] Product Owner: Aprobacion funcional final registrada.

## 2) Checklist de salida (T0)
- [ ] Ops Lead: Deploy API y Worker completado.
- [ ] Ops Lead: Verificacion de logs sin errores bloqueantes en primeros 15 minutos.
- [ ] QA Lead: Smoke post-deploy exitoso (settings, companies, statistics, export).
- [ ] Tech Lead: Confirmacion de triggers activos y scheduler operativo.
- [ ] Product Owner: Comunicacion de go-live enviada.

## 3) Criterio de rollback
Disparar rollback inmediato si ocurre alguno:
1. Error P1 de disponibilidad de API por mas de 5 minutos.
2. Worker sin ejecucion de ciclos por mas de 15 minutos.
3. Falla de persistencia de adjuntos en mas del 30% de intentos durante ventana inicial.
4. Error de datos critico (corrupcion o escritura en ruta incorrecta).
5. Incidente de seguridad (exposicion de secretos/datos sensibles).

## 4) Procedimiento de rollback
1. Ops Lead: detener despliegue en curso.
2. Ops Lead: detener servicio Worker actual.
3. Ops Lead: restaurar binarios y configuracion de version previa validada.
4. Ops Lead: reiniciar servicio Worker y API en version previa.
5. Tech Lead: ejecutar smoke minimo de verificacion.
6. QA Lead: confirmar estabilidad post-rollback.
7. Product Owner: comunicar rollback y estado a stakeholders.

## 5) Evidencia requerida de cierre
- Registro de aprobacion Tech Lead / QA Lead / Ops Lead / Product Owner.
- Timestamp de go-live o rollback.
- Resultado de smoke post-despliegue.
- Incidentes ocurridos y accion tomada.

## 6) Ejecucion de validacion pre-go-live (2026-03-31)
- Timestamp de ejecucion: 2026-03-31T17:05:23Z.
- Comando ejecutado:
  - `powershell -ExecutionPolicy Bypass -File scripts/validate-go-live.ps1 -StoragePath "\\localhost\c$\Temp"`
- Resultado consolidado:
  - Gate Graph (`GET /api/health/graph`): `FAIL` (status `0`, API no alcanzable en este entorno).
  - Gate Storage/UNC (`POST /api/settings/storage-access/check`): `FAIL` (status `0`, API no alcanzable en este entorno).
  - Resultado general: `FAIL`.
- Evidencia generada:
  - `artifacts/dev-logs/go-live-validation-20260331-120523.out.log`
  - `artifacts/dev-logs/go-live-validation-20260331-120523.err.log`
  - `artifacts/dev-logs/go-live-validation-20260331-120523.summary.json`
- Decision de salida:
  - `NO-GO` en este entorno hasta ejecutar validacion con API activa + credenciales Graph reales + ruta UNC operativa.
