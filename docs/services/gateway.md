# API Gateway

**Puerto:** 8080 | **Tecnología:** .NET 9 + YARP (Yet Another Reverse Proxy)

## Responsabilidad

Punto de entrada único del sistema. Centraliza:

- **Autenticación JWT** — valida el token en cada request antes de hacer proxy
- **Rate Limiting** — máximo 100 req/min por cliente (basado en IP / `sub` claim)
- **Reverse Proxy** — enruta hacia los microservicios internos

## Rutas configuradas

| Patrón de entrada | Destino interno | Descripción |
|---|---|---|
| `/api/credits/**` | `http://credits-api:5001/api/v1/credits/**` | CRUD de créditos |
| `/api/clients/**` | `http://credits-api:5001/api/v1/clients/**` | CRUD de clientes |
| `/api/audit/**` | `http://audit-api:5003/api/v1/audit/**` | Consulta de auditoría |
| `/api/batch/**` | `http://batch-processor:5005/api/v1/batch/**` | Jobs batch |
| `/hangfire/**` | `http://batch-processor:5005/hangfire/**` | Dashboard Hangfire |

> Risk API y Notifications API **no** están expuestas directamente — solo se comunican via RabbitMQ o HTTP interno entre servicios.

## Autenticación JWT

El Gateway carga el secreto JWT desde **HashiCorp Vault** al arrancar. Los tokens se validan con:

| Claim | Valor esperado |
|---|---|
| `iss` | `kriteriom-auth` |
| `aud` | `kriteriom-api` |
| `exp` | No expirado |
| `role` | `User` / `Analyst` / `Admin` |

El header `Authorization: Bearer {token}` es obligatorio en todos los endpoints excepto `/health`.

## Rate Limiting

Política fija: **100 requests/minuto** por IP. Las respuestas al superar el límite devuelven `429 Too Many Requests` con header `Retry-After`.

## Headers propagados hacia los servicios internos

| Header | Descripción |
|---|---|
| `X-Correlation-Id` | UUID de correlación generado en el Gateway si no viene en el request |
| `X-Forwarded-For` | IP del cliente original |
| `X-User-Id` | Claim `sub` extraído del JWT |
| `X-User-Role` | Claim `role` extraído del JWT |

## Configuración (appsettings / Vault)

```json
{
  "Jwt": {
    "Issuer": "kriteriom-auth",
    "Audience": "kriteriom-api",
    "SecretKey": "← cargado desde Vault"
  },
  "RateLimit": {
    "RequestsPerMinute": 100
  },
  "ReverseProxy": {
    "Routes": { "...": "..." },
    "Clusters": { "...": "..." }
  }
}
```
