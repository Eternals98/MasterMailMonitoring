# Dia 2 - Web Checklist Manual

## Preparacion
- [ ] Crear `MailMonitor.Web/.env` desde `.env.example` y apuntar `VITE_API_BASE_URL` a la API.
- [ ] Levantar API y web (`npm run dev`).

## Home + Layout + Router
- [ ] Abrir `/` y validar menu lateral visible.
- [ ] Navegar desde menu a `/settings`, `/companies`, `/graph-settings`, `/monitoring`.
- [ ] Confirmar que el titulo de pagina cambia por ruta.
- [ ] Confirmar respuesta responsive en desktop y mobile.

## Settings
- [ ] En `/settings`, validar estado `loading` inicial.
- [ ] Forzar error API (URL incorrecta) y validar `ErrorMessage` + boton reintentar.
- [ ] Con API disponible, validar lectura de `baseStorageFolder`, `processingTag`, `mailSubjectKeywords`.
- [ ] Intentar guardar con ruta vacia y validar foco en primer error.
- [ ] Guardar con ruta valida y validar toast de exito.
- [ ] Durante guardado, validar boton deshabilitado.

## Companies
- [ ] En `/companies`, validar carga inicial de tabla.
- [ ] Probar filtros `name` y `mail`.
- [ ] Crear company valida con campos multivalor (`mailBox`, `fileTypes`, `attachmentKeywords`).
- [ ] Editar company existente desde accion `Editar`.
- [ ] Probar validaciones frontend:
  - [ ] Correo invalido.
  - [ ] Rutas invalidas en `storageFolder`/`reportOutputFolder`.
  - [ ] `startFrom` con fecha invalida.
  - [ ] Duplicados en listas multivalor.
- [ ] Confirmar foco automatico en primer error de validacion.
- [ ] Eliminar company y validar confirmacion + refresco de lista.
- [ ] Confirmar disable de botones durante submit.

## Graph Settings
- [ ] En `/graph-settings`, validar carga de datos actuales.
- [ ] Confirmar que se muestra `clientSecretMasked`.
- [ ] Validar ayuda visual para `GraphUserScopesJson`.
- [ ] Intentar guardar con JSON de scopes invalido y validar error.
- [ ] Intentar guardar sin secret y validar error + foco.
- [ ] Guardar con secret valido y validar toast de exito.

## Monitoring
- [ ] En `/monitoring`, validar filtros `from`, `to`, `company`, `status`.
- [ ] Aplicar filtros y validar tabla de resultados.
- [ ] Validar KPI: `Total`, `Procesados`, `Ignorados`.
- [ ] Probar estado sin resultados (`EmptyState`).
- [ ] Exportar Excel y validar descarga de archivo.
- [ ] Durante exportacion, validar boton deshabilitado.

## Manejo central de errores
- [ ] Validar mensajes para 400 con detalle de validacion.
- [ ] Validar mensajes para 404.
- [ ] Validar mensajes para 500.
- [ ] Validar mensajes para red/timeout.

## Componentes reutilizables
- [ ] Validar visualizacion de `Loading` en cargas.
- [ ] Validar visualizacion de `ErrorMessage` en errores.
- [ ] Validar visualizacion de `EmptyState` sin data.
- [ ] Validar `Toast` de exito y error con autocierre.