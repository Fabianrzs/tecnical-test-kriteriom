# Audit API

**Puerto:** 5003 | **Base de datos:** `audit_db` (PostgreSQL 16)

## Responsabilidad

Registra de forma **inmutable** todos los eventos de integración del sistema. Actúa como ledger de auditoría — una vez insertado un registro, nunca se modifica ni elimina.

## Endpoints REST

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/v1/audit` | Lista registros paginados (filtros: `entityId`, `eventType`, `from`, `to`) |
| `GET` | `/api/v1/audit/{id}` | Obtiene un registro por ID |

Accesible vía Gateway en `/api/audit`.

## Modelo de datos

```
AuditRecords
  id          (uuid PK)
  eventId     (uuid — del integration event)
  eventType   (string — p.ej. "CreditCreated", "RiskAssessed")
  entityId    (uuid — creditId o notificationId)
  entityType  (string)
  payload     (jsonb — snapshot completo del evento)
  occurredAt  (timestamp)
  processedAt (timestamp)
```

El campo `eventId` es único — la deduplicación garantiza que el mismo evento no se registra dos veces aunque llegue dos veces por at-least-once delivery.

## Eventos consumidos

| Evento | Acción |
|---|---|
| `CreditCreatedIntegrationEvent` | INSERT en `AuditRecords` |
| `CreditUpdatedIntegrationEvent` | INSERT en `AuditRecords` |
| `RiskAssessedIntegrationEvent` | INSERT en `AuditRecords` |
| `NotificationDeliveryFailedEvent` | INSERT en `AuditRecords` |
| `NotificationPermanentlyFailedEvent` | INSERT en `AuditRecords` con alerta |

## Garantías

- **Inmutabilidad** — No existe endpoint de UPDATE ni DELETE.
- **Deduplicación** — Antes de insertar se verifica `SELECT 1 FROM AuditRecords WHERE eventId = ?`.
- **Trazabilidad** — El `payload` almacena el snapshot completo del evento, incluyendo `correlationId` para correlacionar con Jaeger.
