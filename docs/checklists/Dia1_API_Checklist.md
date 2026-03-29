# Checklist de cierre Dia 1 - Backend API

Objetivo: validar cada endpoint D1 con evidencia reproducible.

## Convenciones
- Status esperado: codigo HTTP esperado.
- Evidencia: captura Swagger, resultado de `MailMonitor.Api.http` o salida de consola.

## 1) Settings

- [ ] `GET /api/settings`
  - Status esperado: `200 OK`
  - Response ejemplo:
    ```json
    {
      "baseStorageFolder": "C:\\MailMonitor\\Storage",
      "mailSubjectKeywords": ["factura", "orden"],
      "processingTag": "ONBASE"
    }
    ```
  - Evidencia: ______________________________

- [ ] `PUT /api/settings`
  - Status esperado: `204 No Content`
  - Request ejemplo:
    ```json
    {
      "baseStorageFolder": "C:\\MailMonitor\\Storage"
    }
    ```
  - Evidencia: ______________________________

- [ ] `PUT /api/settings` (validacion)
  - Status esperado: `400 Bad Request`
  - Request ejemplo:
    ```json
    {
      "baseStorageFolder": ""
    }
    ```
  - Evidencia: ______________________________

## 2) Graph Settings

- [ ] `GET /api/graph-settings`
  - Status esperado: `200 OK` o `404 Not Found`
  - Response ejemplo (200):
    ```json
    {
      "instance": "https://login.microsoftonline.com/",
      "clientId": "11111111-2222-3333-4444-555555555555",
      "tenantId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "clientSecretMasked": "********cret",
      "graphUserScopesJson": "[\"Mail.Read\"]"
    }
    ```
  - Evidencia: ______________________________

- [ ] `PUT /api/graph-settings`
  - Status esperado: `204 No Content`
  - Evidencia: ______________________________

- [ ] `PUT /api/graph-settings` (JSON scopes invalido)
  - Status esperado: `400 Bad Request`
  - Evidencia: ______________________________

## 3) Companies

- [ ] `GET /api/companies` (filtros `name`, `mail`)
  - Status esperado: `200 OK`
  - Evidencia: ______________________________

- [ ] `POST /api/companies`
  - Status esperado: `201 Created`
  - Evidencia: ______________________________

- [ ] `POST /api/companies` (payload invalido)
  - Status esperado: `400 Bad Request`
  - Evidencia: ______________________________

- [ ] `GET /api/companies/{id}`
  - Status esperado: `200 OK`
  - Evidencia: ______________________________

- [ ] `GET /api/companies/{id}` (inexistente)
  - Status esperado: `404 Not Found`
  - Evidencia: ______________________________

- [ ] `PUT /api/companies/{id}`
  - Status esperado: `204 No Content`
  - Evidencia: ______________________________

- [ ] `PUT /api/companies/{id}` (id ruta/body distinto)
  - Status esperado: `400 Bad Request`
  - Evidencia: ______________________________

- [ ] `PUT /api/companies/{id}` (inexistente)
  - Status esperado: `404 Not Found`
  - Evidencia: ______________________________

- [ ] `DELETE /api/companies/{id}`
  - Status esperado: `204 No Content`
  - Evidencia: ______________________________

- [ ] `DELETE /api/companies/{id}` (inexistente)
  - Status esperado: `404 Not Found`
  - Evidencia: ______________________________

## 4) Triggers

- [ ] `GET /api/triggers`
  - Status esperado: `200 OK`
  - Evidencia: ______________________________

- [ ] `POST /api/triggers`
  - Status esperado: `201 Created`
  - Evidencia: ______________________________

- [ ] `POST /api/triggers` (cron invalido)
  - Status esperado: `400 Bad Request`
  - Evidencia: ______________________________

- [ ] `GET /api/triggers/{id}`
  - Status esperado: `200 OK`
  - Evidencia: ______________________________

- [ ] `GET /api/triggers/{id}` (inexistente)
  - Status esperado: `404 Not Found`
  - Evidencia: ______________________________

- [ ] `PUT /api/triggers/{id}`
  - Status esperado: `204 No Content`
  - Evidencia: ______________________________

- [ ] `PUT /api/triggers/{id}` (cron invalido)
  - Status esperado: `400 Bad Request`
  - Evidencia: ______________________________

- [ ] `PUT /api/triggers/{id}` (inexistente)
  - Status esperado: `404 Not Found`
  - Evidencia: ______________________________

- [ ] `DELETE /api/triggers/{id}`
  - Status esperado: `204 No Content`
  - Evidencia: ______________________________

- [ ] `DELETE /api/triggers/{id}` (inexistente)
  - Status esperado: `404 Not Found`
  - Evidencia: ______________________________

## 5) Email Statistics (MVP)

- [ ] `GET /api/email-statistics`
  - Status esperado: `200 OK`
  - Verificar orden descendente por `date`
  - Evidencia: ______________________________

- [ ] `GET /api/email-statistics?from&to&company&processed`
  - Status esperado: `200 OK`
  - Verificar filtros aplicados
  - Evidencia: ______________________________

- [ ] `GET /api/email-statistics` (rango invalido)
  - Status esperado: `400 Bad Request`
  - Evidencia: ______________________________

## 6) Reports Export (MVP)

- [ ] `GET /api/reports/export`
  - Status esperado: `200 OK`
  - Content-Type: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
  - Verificar descarga de `.xlsx`
  - Evidencia: ______________________________

- [ ] `GET /api/reports/export` (rango invalido)
  - Status esperado: `400 Bad Request`
  - Evidencia: ______________________________

## Cierre D1

- [ ] `MailMonitor.Api.http` ejecutado con casos de exito/error
- [ ] Swagger D1 validado (summary + responses + ejemplos)
- [ ] `dotnet build` sin errores
