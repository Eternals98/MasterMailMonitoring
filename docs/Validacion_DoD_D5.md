# Validacion DoD (8 puntos) - D5-P07

## Fecha de validacion
- 2026-03-30

## Criterio base DoD
Referencia: `docs/Analisis_estado_y_plan_2026-04-01.md` (seccion DoD).

## Matriz de cumplimiento

| # | Punto DoD | Estado | Evidencia |
|---|---|---|---|
| 1 | CRUD completo desde UI para Companies/Settings/Graph/Triggers | PARCIAL (aceptado por alcance congelado) | UI: `MailMonitor.Web/src/App/AppRouter.tsx` (settings/companies/graph-settings/monitoring). Triggers: API disponible en `MailMonitor.Api/Controllers/TriggersController.cs`. Congelamiento: `docs/Alcance_Congelado_Entrega_2026-04-01.md`. |
| 2 | Worker ejecutando en ambiente objetivo con al menos 2 Companies activas | NO VALIDADO | Ensayo local ejecutado en `docs/Ensayo_Operacion_E2E_D5.md`, pero no en ambiente objetivo con 2 companies reales. |
| 3 | Descarga en ruta local y ruta de red validada | NO VALIDADO | Casos manuales Graph/UNC pendientes en `docs/Resultados_QA_Final.md` y `docs/Dia4_E2E_Manual_Cases.md`. |
| 4 | Tag por Company aplicado exitosamente en Graph | NO VALIDADO | Requiere credenciales y buzon real de ambiente objetivo. |
| 5 | Historico y exportacion Excel operativos | CUMPLE | API smoke `ReportExport=PASS` y endpoint stats/export validado en `docs/Resultados_QA_Final.md`. |
| 6 | Logs con trazabilidad por MessageId | CUMPLE (tecnico) | Worker usa scopes de `messageId/company/mailbox` en `MailMonitor.Worker/Worker.cs`; ejecucion de job registrada en `docs/d5_e2e_worker.out.log`. |
| 7 | Suite minima de pruebas pasando en CI/local | CUMPLE | `dotnet test MailMonitor.sln` -> 25/25 OK, evidencia TRX en `MailMonitor.UnitTests/TestResults/D5_Final_All.trx` y `MailMonitor.IntegrationTests/TestResults/D5_Final_All.trx`. |
| 8 | Manual operativo + checklist de recuperacion | CUMPLE | Runbook y checklists: `docs/Dia4_Runbook_Operativo.md`, `docs/checklists/Dia4_QA_Deploy_Checklist.md`, `docs/checklists/GoLive_Rollback_Checklist_D5.md`. |

## Estado consolidado
- Cumple: 4
- Parcial: 1
- No validado: 3

## Decision de salida
- **Go-live condicionado**: solo proceder si se completan en ambiente objetivo los puntos 2, 3 y 4.
- Si no se completan, mantener estado piloto/no-go y ejecutar criterio de rollback definido.
