# Notas de version - 2026-04-01 (D5-P06)

## Resumen
Version de cierre orientada a salida controlada con foco en estabilidad operativa, calidad de API y gobernanza de despliegue.

## Cambios funcionales
1. API de administracion disponible para settings, companies, graph settings y triggers.
2. Consulta de estadisticas y exportacion Excel habilitadas.
3. Consola web funcional para configuracion y monitoreo.
4. Health endpoint para validacion de conectividad Graph.

## Cambios tecnicos
1. Programacion de worker por Quartz con soporte de triggers persistidos.
2. Endurecimiento de mascarado de secretos en respuestas API.
3. Cobertura automatizada ampliada (unit + integration) con regresion para fixes de cierre.
4. Documentacion operacional y de contingencia formalizada.

## Correcciones incluidas en cierre D5
1. Fix critico: `POST /api/triggers` ya no devuelve 500 y responde `201 Created` correctamente.
2. Fix de seguridad: secretos Graph de longitud corta quedan totalmente enmascarados.

## Calidad validada
- Unit tests: 17/17 OK.
- Integration tests: 8/8 OK.
- Smoke manual API: 9/9 checks OK.

## Limitaciones conocidas para go-live
1. Los casos manuales de Graph real y ruta UNC requieren ejecucion en ambiente objetivo.
2. No hay UI de triggers en web para esta release (gestion via API).
3. No se incluye CI/CD full ni observabilidad avanzada en esta version.

## Riesgo residual
- Dependencia de credenciales y permisos reales de Graph en produccion.
- Dependencia de acceso de cuenta de servicio a carpetas de red UNC.

## Recomendacion de despliegue
- Ejecutar salida piloto controlada con monitoreo de primeros ciclos y criterio de rollback activo.
