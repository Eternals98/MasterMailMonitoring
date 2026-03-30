# Dia 4 - Casos E2E manuales (piloto)

Objetivo: validar el flujo extremo a extremo del worker y API con escenarios reales antes del piloto.

## Precondiciones comunes
- API levantada y accesible.
- Worker instalado o ejecutado en modo consola con la misma base SQLite que la API.
- Configuracion de Graph y Companies cargada.
- Carpeta de salida configurada en `BaseStorageFolder` disponible.
- Un buzón de pruebas con permisos de lectura/escritura de categorias.

## Caso 1: Flujo valido (correo procesado correctamente)
- Tipo: valido.
- Datos:
  - Asunto contiene keyword global.
  - Correo con adjuntos que cumplen extension y keyword por company.
  - Carpeta destino accesible.
- Pasos:
  1. Enviar correo de prueba al buzón de la company.
  2. Ejecutar ciclo del worker.
  3. Consultar `GET /api/email-statistics?company=<company>&processed=true`.
  4. Verificar carpeta destino.
  5. Revisar categorias del mensaje en Graph.
- Resultado esperado:
  - Registro con `processed=true`.
  - `attachmentsCount > 0`.
  - Archivos persistidos en disco.
  - Mensaje marcado con `processingTag`.
- Evidencia:
  - Captura API + ruta de archivos + captura de categoria en Graph.

## Caso 2: Flujo no valido (asunto no cumple filtro)
- Tipo: no valido.
- Datos:
  - Asunto sin coincidencia con `MailSubjectKeywords`.
  - Mensaje con adjuntos.
- Pasos:
  1. Enviar correo con asunto no valido.
  2. Ejecutar ciclo del worker.
  3. Consultar `GET /api/email-statistics?processed=false`.
- Resultado esperado:
  - Registro con `processed=false`.
  - `reasonIgnored = "Subject does not match global keywords"`.
  - Sin archivos guardados.
  - Sin categoria de procesamiento agregada.
- Evidencia:
  - JSON de estadistica + evidencia de carpeta sin nuevos archivos.

## Caso 3: Flujo sin adjuntos
- Tipo: sin adjuntos.
- Datos:
  - Asunto valido por keywords.
  - `hasAttachments=false`.
- Pasos:
  1. Enviar correo sin adjuntos.
  2. Ejecutar ciclo del worker.
  3. Consultar `GET /api/email-statistics?processed=false`.
- Resultado esperado:
  - Registro con `processed=false`.
  - `reasonIgnored = "Message has no attachments"`.
  - No se escribe nada en almacenamiento.
- Evidencia:
  - JSON de estadistica + logs del worker.

## Caso 4: Falla de ruta de red (UNC/no disponible)
- Tipo: fallo ruta red.
- Datos:
  - `BaseStorageFolder` o `StorageFolder` apuntando a share no accesible (ejemplo: `\\server-inexistente\mail`).
  - Asunto y adjuntos validos.
- Pasos:
  1. Configurar ruta UNC no disponible.
  2. Ejecutar ciclo del worker.
  3. Revisar `GET /api/email-statistics`.
  4. Revisar logs del worker.
- Resultado esperado:
  - Registro con `processed=false`.
  - `reasonIgnored` indicando error de persistencia.
  - Logs con codigos `STG-404` o `STG-504`.
  - El proceso no se detiene para otras companies/mensajes.
- Evidencia:
  - Log con error + estadistica de mensaje fallido.

## Criterio de aceptacion de piloto
- 4/4 casos ejecutados.
- Ningun crash del worker en ciclo completo.
- Trazabilidad completa de cada caso (API + logs + evidencia de archivos/categorias).
