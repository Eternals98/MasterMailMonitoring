# Checklist Dia 4 - QA + despliegue piloto

Objetivo: cerrar validacion tecnica y operativa antes de salida piloto.

## 1) Pruebas unitarias (P01-P04)
- [ ] P01 validaciones `Company` y `GraphSetting` implementadas.
- [ ] P02 pruebas de filtro de asunto y parsing de keywords implementadas.
- [ ] P03 pruebas de filtro de adjuntos por extension/keywords implementadas.
- [ ] P04 pruebas de sanitizacion de asunto y nombre de archivo implementadas.
- [ ] `dotnet test MailMonitor.UnitTests` en verde.

## 2) Integracion API + SQLite (P05-P07)
- [ ] P05 `GET/PUT /api/settings` cubierto con integracion real (HTTP + SQLite).
- [ ] P06 CRUD `api/companies` cubierto con integracion real.
- [ ] P07 `api/email-statistics` y filtros cubiertos con integracion real.
- [ ] `dotnet test MailMonitor.IntegrationTests` en verde.

## 3) Validacion manual E2E (P08)
- [ ] Caso valido ejecutado y evidenciado.
- [ ] Caso no valido ejecutado y evidenciado.
- [ ] Caso sin adjuntos ejecutado y evidenciado.
- [ ] Caso falla ruta red ejecutado y evidenciado.
- [ ] Evidencias adjuntas (API/logs/archivos/categorias).

## 4) Despliegue worker como servicio (P09)
- [ ] Script `scripts/install-worker-service.ps1` validado en entorno objetivo.
- [ ] Procedimiento de desinstalacion/rollback validado.
- [ ] Servicio queda en `Running` y reinicia automaticamente ante fallo.

## 5) Configuracion por ambiente (P10)
- [ ] Documento de configuracion `Development/QA/Production` aprobado.
- [ ] Variables requeridas cargadas en cada ambiente.
- [ ] Secretos fuera del repositorio y gestionados en vault/variables seguras.

## 6) Runbook operativo (P11)
- [ ] Runbook con arranque, monitoreo, recuperacion y rollback publicado.
- [ ] Equipo de operaciones conoce rutas de logs, endpoints y alertas.
- [ ] Simulacion de incidente y recuperacion completada.

## 7) Go/No-Go piloto
- [ ] Sin bloqueantes abiertos P1.
- [ ] Riesgos residuales documentados y aceptados.
- [ ] Aprobacion tecnica (Dev + QA + Operaciones).
- [ ] Fecha/hora de salida piloto confirmada.
