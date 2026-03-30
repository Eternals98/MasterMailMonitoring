# Ensayo de operacion E2E (D5-P04)

## Fecha
- 2026-03-30

## Objetivo
Validar operacion punta a punta en ambiente local controlado:
1. Arranque de API.
2. Preparacion de configuracion operativa (triggers/companies) via API.
3. Arranque de Worker.
4. Ejecucion de al menos 1 ciclo del job programado.
5. Confirmacion de estabilidad y trazabilidad de logs.

## Escenario de ensayo
- Base SQLite aislada: `docs/d5_e2e_rehearsal.db`.
- API levantada en: `http://127.0.0.1:5090`.
- Trigger de ensayo configurado: `0/15 * * ? * * *`.
- Worker ejecutado contra la misma base.

## Tiempos medidos
- `API_STARTUP_SECONDS=15.57`
- `PREP_SECONDS=4.22`
- `WORKER_WAIT_SECONDS=27.45`
- Tiempo total estimado del ensayo: 47.24 s

## Resultado del ensayo
- Trigger creado correctamente.
- Worker detecto y ejecuto ciclo (`CYCLE_DETECTED=True`).
- El sistema mantuvo estabilidad de proceso durante el ejercicio.
- Logs muestran inicio y fin del ciclo de job sin caida del host.

## Evidencia
- API logs:
  - `docs/d5_e2e_api.out.log`
  - `docs/d5_e2e_api.err.log`
- Worker logs:
  - `docs/d5_e2e_worker.out.log`
  - `docs/d5_e2e_worker.err.log`
- Base de ensayo:
  - `docs/d5_e2e_rehearsal.db`

## Observaciones tecnicas
- En el ciclo de worker se evidencio error de Graph por credenciales no configuradas (`ClientIdRequired`).
- Aun con ese error externo, el ciclo finalizo correctamente y la aplicacion no colapso.
- Esto valida tolerancia operativa basica del host y trazabilidad de falla.

## Criterio de aprobacion del ensayo
- Arranque API: aprobado.
- Provision de trigger via API: aprobado.
- Arranque worker y scheduling: aprobado.
- Ejecucion de ciclo y cierre de job: aprobado.
- Dependencia externa Graph real: pendiente para ambiente de salida.
