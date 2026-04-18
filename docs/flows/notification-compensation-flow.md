# Flujo Completo: Saga de Compensación de Notificaciones

Flujo detallado del mecanismo de reintentos cuando el envío de una notificación falla.

## Diagrama de secuencia

```mermaid
sequenceDiagram
  autonumber
  participant MQ as RabbitMQ<br/>:5672
  participant NO as Notifications API<br/>:5004
  participant PG as PostgreSQL<br/>notifications_db
  participant AU as Audit API<br/>:5003
  participant CH as Canal de notificación<br/>(LogSender / SMTP / SMS)

  MQ->>NO: RiskAssessedIntegrationEvent<br/>{ creditId, clientId, decision, riskScore }

  NO->>NO: ExistsForEventAsync(eventId)
  alt Ya procesado
    NO-->>MQ: ack (idempotente, sin acción)
  else Nuevo evento
    NO->>NO: Construye mensaje según decision:<br/>Approved / Rejected / UnderReview
    NO->>PG: INSERT Notifications { status=Pending, attempts=0 }

    NO->>CH: INotificationSender.SendAsync(message)

    alt Envío exitoso
      NO->>PG: UPDATE status=Sent, sentAt=NOW()
      NO-->>MQ: ack
    else Fallo en envío (intento 1)
      NO->>PG: UPDATE status=Failed, attempts=1
      NO->>MQ: Publish NotificationDeliveryFailedEvent<br/>{ notificationId, attempt: 1 }
      NO-->>MQ: ack (mensaje original)
    end
  end

  loop Compensation (intento 1..4)
    MQ->>NO: NotificationDeliveryFailedEvent { notificationId, attempt }
    NO->>PG: SELECT notification WHERE id = ?
    NO->>CH: INotificationSender.SendAsync(message)

    alt Intento exitoso
      NO->>PG: UPDATE status=Sent, sentAt=NOW()
      NO-->>MQ: ack
    else Sigue fallando y attempt < 5
      NO->>PG: UPDATE attempts = attempt + 1
      NO->>MQ: Publish NotificationDeliveryFailedEvent<br/>{ notificationId, attempt: attempt+1 }
      NO-->>MQ: ack
    end
  end

  Note over NO: Intento 5 — último reintento
  MQ->>NO: NotificationDeliveryFailedEvent { notificationId, attempt: 5 }
  NO->>CH: INotificationSender.SendAsync(message)

  alt Éxito en último intento
    NO->>PG: UPDATE status=Sent
    NO-->>MQ: ack
  else Fallo definitivo
    NO->>PG: UPDATE status=PermanentlyFailed, attempts=5
    NO->>MQ: Publish NotificationPermanentlyFailedEvent<br/>{ notificationId, creditId, clientId, reason }
    NO-->>MQ: ack

    MQ->>AU: NotificationPermanentlyFailedEvent
    AU->>PG: INSERT AuditRecords<br/>{ eventType="NotificationPermanentlyFailed", payload=... }
    AU-->>MQ: ack
  end
```

## Estados de una notificación

```
Pending → Sent           (envío exitoso en cualquier intento)
Pending → Failed         (fallo inicial, entra en compensación)
Failed  → Sent           (recuperado en reintento 1-4)
Failed  → PermanentlyFailed  (agotó MaxRetries = 5)
```

## Configuración

| Parámetro | Valor |
|---|---|
| `MaxRetries` | 5 |
| Sender activo | `LogNotificationSender` (simulado) |
| Interface reemplazable | `INotificationSender` |

## Observabilidad

- Cada intento queda registrado en `notifications_db` con el contador `attempts`.
- El fallo permanente genera un registro en `audit_db` con el snapshot completo.
- Las trazas de Jaeger muestran el span completo incluyendo todos los reintentos.
