# D5-P01 - Triage final de bugs abiertos (2026-03-29)

## Resumen
- Bugs abiertos revisados: 4
- Criticos (P1): 2
- Criticos corregidos en esta actividad: 2
- Bloqueantes abiertos al cierre: 0

## Clasificacion y estado
| ID | Hallazgo | Severidad | Evidencia | Estado |
|---|---|---|---|---|
| BUG-D5-001 | `GET /api/graph-settings` exponia el secreto completo cuando `ClientSecret` tenia 4 caracteres o menos. Riesgo de exposicion de credenciales. | P1 - Critico | `MailMonitor.Api/Controllers/GraphSettingsController.cs` (metodo `MaskSecret`) | Cerrado |
| BUG-D5-002 | No existia prueba automatizada para enmascarado de secretos Graph (riesgo de regresion de seguridad). | P2 - Alto | No habia pruebas para `/api/graph-settings` en `MailMonitor.IntegrationTests/Api` | Cerrado |
| BUG-D5-003 | Persisten archivos `TODO.md` en contratos de API como deuda de limpieza documental. No impacta ejecucion ni seguridad. | P4 - Bajo | `MailMonitor.Api/Contracts/*/TODO.md` | Abierto (fuera de alcance de D5-P01) |
| BUG-D5-004 | `POST /api/triggers` devolvia HTTP 500 por fallo de resolucion de ruta en respuesta `Created`. Impacta flujo de configuracion operativa. | P1 - Critico | `MailMonitor.Api/Controllers/TriggersController.cs` + ensayo en `docs/d5_e2e_api.out.log` | Cerrado |

## Correcciones aplicadas
1. Se cambio `MaskSecret` para enmascarar completamente secretos de longitud menor o igual a 4.
2. Se agregaron pruebas de integracion para validar:
   - secreto corto enmascarado completo;
   - secreto largo exponiendo solo ultimos 4 caracteres.
3. Se corrigio `POST /api/triggers` para responder `201 Created` usando ruta nombrada valida.
4. Se agrego prueba de integracion para creacion y consulta de trigger:
   - `MailMonitor.IntegrationTests/Api/TriggersApiIntegrationTests.cs`.

## Verificacion de cierre
- Comando: `dotnet test MailMonitor.IntegrationTests/MailMonitor.IntegrationTests.csproj --configuration Debug --logger "trx;LogFileName=GraphSettings_D5_P01.trx"`
- Comando: `dotnet test MailMonitor.IntegrationTests/MailMonitor.IntegrationTests.csproj --configuration Debug --logger "trx;LogFileName=D5_PostFix_Integration.trx"`
- Resultado: 8/8 pruebas de integracion exitosas, 0 fallas.
- Evidencia:
  - `MailMonitor.IntegrationTests/TestResults/GraphSettings_D5_P01.trx`
  - `MailMonitor.IntegrationTests/TestResults/D5_PostFix_Integration.trx`.
