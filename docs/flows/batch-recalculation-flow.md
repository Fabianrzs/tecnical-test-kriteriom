# Flujo Completo: Recálculo Batch de Estados

Flujo end-to-end del job de recálculo periódico que ajusta estados de créditos según reglas de negocio.

## Diagrama de secuencia

```mermaid
sequenceDiagram
  autonumber
  participant HF as Hangfire Scheduler
  participant BP as Batch Processor<br/>:5005
  participant CA as Credits API<br/>:5001
  participant PG as PostgreSQL<br/>credits_db
  participant MQ as RabbitMQ<br/>:5672
  participant AU as Audit API<br/>:5003

  Note over HF: Cron: cada 6 horas<br/>O disparo manual POST /api/batch/recalculate

  HF->>BP: Ejecuta RecalculateCreditStatusesJob
  BP->>BP: Adquiere distributed lock<br/>(evita ejecución paralela)

  BP->>CA: GET /api/v1/credits?status=Pending,UnderReview&page=1&size=100
  CA->>PG: SELECT credits WHERE status IN (Pending, UnderReview)
  PG-->>CA: [credits...]
  CA-->>BP: 200 OK [creditDtos...]

  loop Por cada crédito
    BP->>BP: Evalúa reglas:
    Note over BP: Pending + createdAt > 72h     → Expired<br/>riskScore ≥ 750 + totalDti ≤ 30%  → Approved<br/>riskScore < 400                    → Rejected

    alt Estado cambia
      BP->>CA: PUT /api/v1/credits/{id}/status<br/>{ newStatus, reason }
      CA->>PG: UPDATE Credits SET status = ?, updatedAt = NOW()
      CA->>PG: INSERT OutboxMessages (CreditUpdatedIntegrationEvent)
      CA->>PG: COMMIT
      CA-->>BP: 200 OK
    else Sin cambio
      Note over BP: No action
    end
  end

  BP->>MQ: Publish RecalculateCreditStatusesRequestedEvent<br/>{ jobId, processedCount, updatedCount }

  MQ->>CA: RecalculateCreditStatusesRequestedEvent
  CA->>CA: RecalculateCreditStatusesConsumer<br/>→ RecalculateCreditStatusesCommandHandler
  Note over CA: Aplica mismas reglas internamente<br/>(segunda pasada de consistencia)

  Note over CA: OutboxProcessorService (background)
  CA->>PG: SELECT OutboxMessages WHERE published = false
  CA->>MQ: Publish CreditUpdatedIntegrationEvent (×N)
  CA->>PG: UPDATE OutboxMessages SET published = true

  MQ->>AU: CreditUpdatedIntegrationEvent (×N)
  AU->>AU: Verifica EventId duplicado
  AU->>PG: INSERT AuditRecords (×N)
  AU-->>MQ: ack

  BP->>BP: Libera distributed lock
  BP->>BP: Registra resultado en Hangfire:<br/>{ jobId, processedCount, updatedCount, duration }
```

## Reglas de negocio aplicadas

| Condición | Estado anterior | Estado nuevo |
|---|---|---|
| `createdAt` > 72 h sin evaluación de riesgo | `Pending` | `Expired` |
| `riskScore ≥ 750` y `totalDti ≤ 30 %` | `UnderReview` | `Approved` |
| `riskScore < 400` | `UnderReview` | `Rejected` |
| `riskScore ≥ 600` y `totalDti ≤ 50 %` | — | Se mantiene `UnderReview` |

## Garantías

- **Idempotencia del job** — Si el job se ejecuta dos veces, la segunda iteración no cambia nada (los estados ya están actualizados).
- **Distributed lock de Hangfire** — Previene ejecuciones paralelas del mismo job.
- **Trazabilidad** — Cada crédito actualizado genera un `CreditUpdatedIntegrationEvent` auditado en `audit_db`.
