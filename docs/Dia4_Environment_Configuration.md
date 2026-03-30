# Dia 4 - Configuracion por ambiente

Este documento define la configuracion requerida para `Development`, `QA` y `Production`.

## Variables de entorno de host
- API:
  - `ASPNETCORE_ENVIRONMENT=Development|QA|Production`
- Worker:
  - `DOTNET_ENVIRONMENT=Development|QA|Production`

## Variables requeridas (API + Worker)
Se recomienda configurar con variables de entorno usando `__` para secciones anidadas:

- `Persistence__ConfigurationDbPath`
- `Graph__Instance`
- `Graph__ClientId`
- `Graph__TenantId`
- `Graph__ClientSecret`
- `Graph__Scopes__0` (y siguientes si aplica)
- `Storage__MaxRetries`
- `Storage__BaseDelayMilliseconds`
- `Storage__MaxDelayMilliseconds`

Variables exclusivas/relevantes del worker:
- `Scheduling__TimeZoneId`
- `Scheduling__FallbackCronExpression`

## Baseline sugerido por ambiente

| Clave | Development | QA | Production |
|---|---|---|---|
| `Persistence__ConfigurationDbPath` | `mailmonitor.dev.db` local | ruta local dedicada QA | ruta local dedicada PROD (disco resiliente) |
| `Graph__Instance` | `https://login.microsoftonline.com/` | igual | igual |
| `Graph__ClientId` | app de dev | app de QA | app de prod |
| `Graph__TenantId` | tenant de dev | tenant QA | tenant prod |
| `Graph__ClientSecret` | secreto dev | secreto QA (vault) | secreto prod (vault) |
| `Graph__Scopes__0` | `https://graph.microsoft.com/.default` | igual | igual |
| `Scheduling__TimeZoneId` | `America/New_York` | `America/New_York` | zona oficial operativa |
| `Scheduling__FallbackCronExpression` | `0 0/5 * ? * * *` | `0 0/10 * ? * * *` | segun ventana operacional |
| `Storage__MaxRetries` | `4` | `3` | `3-5` |
| `Storage__BaseDelayMilliseconds` | `250` | `300` | `300` |
| `Storage__MaxDelayMilliseconds` | `3000` | `4000` | `4000-8000` |

## Reglas de seguridad
- No persistir secretos (`Graph__ClientSecret`) en repositorio.
- Usar gestor de secretos (Key Vault/Secret Manager/variables protegidas del pipeline).
- Rotar secretos antes de paso a piloto y registrar fecha de rotacion.

## Validacion minima por despliegue
1. `GET /api/settings` responde `200`.
2. `GET /api/graph-settings` devuelve configuracion esperada (secret enmascarado).
3. Worker inicia y ejecuta al menos un ciclo sin excepciones criticas.
4. Se escribe estadistica en `EmailStatistics` para caso controlado.
