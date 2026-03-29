# Checklist de cierre Día 1 — API

> Objetivo: validar funcionalmente cada endpoint del Día 1 con evidencia reproducible.

## Convenciones
- **Status esperado**: código HTTP de éxito o error esperado.
- **Payload ejemplo**: request/response mínimo para validar el caso.
- **Evidencia**: pega captura de Swagger, salida de `MailMonitor.Api.http`, o log de consola.

---

## 1) Settings

- [ ] `GET /api/settings`
  - Status esperado: `200 OK`
  - Payload ejemplo (response):
    ```json
    {
      "baseStorageFolder": "C:\\MailMonitor\\Storage",
      "mailSubjectKeywords": ["factura", "orden"],
      "processingTag": "PROCESSED"
    }
    ```
  - Evidencia: ______________________________

- [ ] `PUT /api/settings`
  - Status esperado: `204 No Content`
  - Payload ejemplo (request):
    ```json
    {
      "baseStorageFolder": "C:\\MailMonitor\\Storage"
    }
    ```
  - Evidencia: ______________________________

- [ ] `PUT /api/settings` (error validación)
  - Status esperado: `400 Bad Request`
  - Payload ejemplo (request):
    ```json
    {
      "baseStorageFolder": ""
    }
    ```
  - Evidencia: ______________________________

## 2) Graph Settings

- [ ] `GET /api/graph-settings`
  - Status esperado: `200 OK` o `404 Not Found` (si no existe configuración inicial)
  - Payload ejemplo (response 200):
    ```json
    {
      "instance": "https://login.microsoftonline.com/",
      "clientId": "11111111-2222-3333-4444-555555555555",
      "tenantId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "clientSecret": "********cret",
      "graphUserScopesJson": "[\"Mail.Read\"]"
    }
    ```
  - Evidencia: ______________________________

- [ ] `PUT /api/graph-settings`
  - Status esperado: `204 No Content`
  - Payload ejemplo (request):
    ```json
    {
      "instance": "https://login.microsoftonline.com/",
      "clientId": "11111111-2222-3333-4444-555555555555",
      "tenantId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "clientSecret": "super-secret-value",
      "graphUserScopesJson": "[\"Mail.Read\",\"Mail.ReadWrite\"]"
    }
    ```
  - Evidencia: ______________________________

- [ ] `PUT /api/graph-settings` (JSON inválido)
  - Status esperado: `400 Bad Request`
  - Payload ejemplo (request):
    ```json
    {
      "instance": "https://login.microsoftonline.com/",
      "clientId": "11111111-2222-3333-4444-555555555555",
      "tenantId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "clientSecret": "super-secret-value",
      "graphUserScopesJson": "{invalid-json}"
    }
    ```
  - Evidencia: ______________________________

## 3) Companies

- [ ] `GET /api/companies`
  - Status esperado: `200 OK`
  - Payload ejemplo (response): lista JSON de compañías
  - Evidencia: ______________________________

- [ ] `POST /api/companies`
  - Status esperado: `201 Created`
  - Payload ejemplo: ver `MailMonitor.Api.http`
  - Evidencia: ______________________________

- [ ] `GET /api/companies/{id}`
  - Status esperado: `200 OK`
  - Payload ejemplo: detalle de compañía
  - Evidencia: ______________________________

- [ ] `GET /api/companies/{id}` (id inexistente)
  - Status esperado: `404 Not Found`
  - Evidencia: ______________________________

- [ ] `PUT /api/companies/{id}`
  - Status esperado: `204 No Content`
  - Payload ejemplo: body con mismo `id` de ruta
  - Evidencia: ______________________________

- [ ] `PUT /api/companies/{id}` (id ruta/body distinto)
  - Status esperado: `400 Bad Request`
  - Evidencia: ______________________________

- [ ] `DELETE /api/companies/{id}`
  - Status esperado: `204 No Content` / `404 Not Found` si no existe
  - Evidencia: ______________________________

## 4) Triggers

- [ ] `GET /api/triggers`
  - Status esperado: `200 OK`
  - Evidencia: ______________________________

- [ ] `POST /api/triggers`
  - Status esperado: `201 Created`
  - Payload ejemplo:
    ```json
    {
      "name": "Daily Report",
      "cronExpression": "0 0 * * *"
    }
    ```
  - Evidencia: ______________________________

- [ ] `POST /api/triggers` (cron inválido)
  - Status esperado: `400 Bad Request`
  - Evidencia: ______________________________

- [ ] `GET /api/triggers/{id}`
  - Status esperado: `200 OK` / `404 Not Found`
  - Evidencia: ______________________________

- [ ] `PUT /api/triggers/{id}`
  - Status esperado: `204 No Content` / `404 Not Found`
  - Evidencia: ______________________________

- [ ] `DELETE /api/triggers/{id}`
  - Status esperado: `204 No Content` / `404 Not Found`
  - Evidencia: ______________________________

## 5) Email Statistics (MVP)

- [ ] `GET /api/email-statistics`
  - Status esperado: `200 OK`
  - Verificar orden: `date` descendente
  - Evidencia: ______________________________

- [ ] `GET /api/email-statistics?from&to&company&processed`
  - Status esperado: `200 OK`
  - Verificar filtros aplicados
  - Evidencia: ______________________________

- [ ] `GET /api/email-statistics` (rango inválido)
  - Status esperado: `400 Bad Request`
  - Ejemplo: `from > to`
  - Evidencia: ______________________________

## 6) Reports Export (MVP)

- [ ] `GET /api/reports/export`
  - Status esperado: `200 OK`
  - Content-Type esperado: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
  - Verificar descarga de archivo `.xlsx`
  - Evidencia: ______________________________

- [ ] `GET /api/reports/export` (rango inválido)
  - Status esperado: `400 Bad Request`
  - Evidencia: ______________________________

---

## Evidencia final (cierre Día 1)
- [ ] Colección HTTP (`MailMonitor.Api.http`) ejecutada
- [ ] Swagger actualizado (summary + códigos + ejemplos)
- [ ] Endpoints MVP de estadísticas/export operativos
- [ ] Sin errores de compilación en `dotnet build`
