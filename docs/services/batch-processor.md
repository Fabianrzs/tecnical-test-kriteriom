# Batch Processor

**Puerto:** 5005 | **Base de datos:** `batch_db` (PostgreSQL 16 + Hangfire)

## Responsabilidad

Ejecuta jobs periódicos para mantener la consistencia del portafolio de créditos:

1. **Recalcular estados** — aplica reglas de negocio sobre todos los créditos activos.
2. **Limpiar outbox** — elimina mensajes procesados con más de 7 días de antigüedad.

## Jobs configurados (Hangfire)

| Job | Cron | Descripción |
|---|---|---|
| `RecalculateCreditStatusesJob` | `0 */6 * * *` (cada 6 h) | Consulta créditos, aplica reglas, actualiza estados |
| `CleanOutboxJob` | `0 3 * * *` (diario 03:00) | Purga outbox messages procesados |

## Flujo de recálculo

```
Hangfire Scheduler → RecalculateCreditStatusesJob
  │  (1) GET /api/v1/credits?status=Pending,UnderReview → Credits API
  │  (2) Por cada crédito:
  │      - Pending > 72 h sin respuesta de riesgo → Expired
  │      - riskScore ≥ 750 y totalDti ≤ 30 %     → Approved
  │      - riskScore < 400                         → Rejected
  │  (3) PUT /api/v1/credits/{id}/status → Credits API
  │  (4) Publish RecalculateCreditStatusesRequestedEvent → RabbitMQ
  │      (Credits API Consumer aplica el mismo handler internamente)
  └──────────────────────────────────────────────────────────────────
```

## Endpoints REST

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/v1/batch/recalculate` | Dispara manualmente el job de recálculo |
| `GET` | `/api/v1/batch/jobs` | Lista el historial de jobs ejecutados |
| `GET` | `/hangfire` | Dashboard Hangfire (solo rol Admin) |

Accesible vía Gateway en `/api/batch` y `/hangfire`.

## Acceso al Dashboard

```
URL: http://localhost:8080/hangfire
Usuario: admin (verificado por JWT con rol Admin)
```

## Patrones técnicos

- **Hangfire** con PostgreSQL como store — jobs sobreviven reinicios del proceso
- **Idempotencia de jobs** — cada ejecución consulta el estado actual antes de actualizar
- **Circuit Breaker** en llamadas HTTP a Credits API
- **Distributed lock** — Hangfire evita ejecución paralela del mismo job
