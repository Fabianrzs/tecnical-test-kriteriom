# Notifications API

**Puerto:** 5004 | **Base de datos:** `notifications_db` (PostgreSQL 16)

## Responsabilidad

Envía notificaciones al usuario sobre el resultado de su solicitud de crédito. Implementa una **saga de compensación** que reintenta el envío hasta 5 veces en caso de fallo.

## Mensajes según decisión de riesgo

| Decisión | Asunto | Mensaje |
|---|---|---|
| `Approved` | ¡Crédito Aprobado! | "Su solicitud de crédito ha sido aprobada. Score: {score}" |
| `Rejected` | Solicitud Rechazada | "Lo sentimos, su solicitud no fue aprobada. Motivo: {reason}" |
| `UnderReview` | En Revisión Manual | "Su solicitud está siendo revisada por un analista." |

## Sender

En esta versión se usa `LogNotificationSender` (logging estructurado). La interfaz `INotificationSender` permite reemplazarlo por SMTP, SMS u otro canal sin modificar la lógica de negocio.

## Flujo con saga de compensación

```
RabbitMQ → CreditCreatedConsumer / RiskAssessedConsumer
  │  (1) ExistsForEventAsync(eventId) → si ya procesado: ack y salir
  │  (2) Construye mensaje según Decision
  │  (3) INotificationSender.SendAsync()
  │
  ├─ Éxito:
  │    UPDATE notifications SET status = Sent
  │    ack → RabbitMQ
  │
  └─ Fallo:
       UPDATE notifications SET status = Failed, attempts = 1
       Publish NotificationDeliveryFailedEvent { notificationId, attempt: 1 }

Compensation loop (hasta MaxRetries = 5):
  RabbitMQ → NotificationDeliveryFailedConsumer
    │  Reintenta INotificationSender.SendAsync()
    ├─ Éxito:   UPDATE status = Sent  →  ack
    └─ Fallo:
         attempt < 5:  Publish NotificationDeliveryFailedEvent { attempt + 1 }
         attempt = 5:  Publish NotificationPermanentlyFailedEvent
                       UPDATE status = PermanentlyFailed
                       Audit API registra el fallo permanente
```

## Modelo de datos

```
Notifications
  id             (uuid PK)
  eventId        (uuid — para deduplicación)
  creditId       (uuid)
  clientId       (uuid)
  decision       (enum: Approved/Rejected/UnderReview)
  message        (text)
  status         (enum: Pending/Sent/Failed/PermanentlyFailed)
  attempts       (int — máx 5)
  createdAt
  sentAt?
```

## Eventos consumidos

| Evento | Acción |
|---|---|
| `CreditCreatedIntegrationEvent` | Crea notificación en estado `Pending` |
| `RiskAssessedIntegrationEvent` | Envía notificación con la decisión final |
| `NotificationDeliveryFailedEvent` | Reintenta el envío (compensation) |

## Eventos publicados

| Evento | Cuándo |
|---|---|
| `NotificationDeliveryFailedEvent` | Fallo de envío, `attempt < MaxRetries` |
| `NotificationPermanentlyFailedEvent` | Se alcanzó `MaxRetries = 5` |
