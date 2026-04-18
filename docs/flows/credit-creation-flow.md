# Flujo Completo: Creación de Crédito

Flujo end-to-end desde que el usuario solicita un crédito hasta que recibe la notificación del resultado.

## Diagrama de secuencia

```mermaid
sequenceDiagram
  autonumber
  actor User as Usuario
  participant GW as API Gateway<br/>:8080
  participant CA as Credits API<br/>:5001
  participant Redis as Redis<br/>:6379
  participant PG as PostgreSQL<br/>credits_db
  participant MQ as RabbitMQ<br/>:5672
  participant RA as Risk API<br/>:5002
  participant AU as Audit API<br/>:5003
  participant NO as Notifications API<br/>:5004

  User->>GW: POST /api/credits<br/>Authorization: Bearer {JWT}<br/>Idempotency-Key: {uuid}

  GW->>GW: Valida JWT<br/>Verifica rate limit (100 req/min)
  GW->>CA: POST /api/v1/credits (proxy)

  Note over CA: Pipeline MediatR:<br/>Logging → Caching → Validation

  CA->>Redis: GET idempotency:{key}
  Redis-->>CA: null (primera vez)

  CA->>PG: SELECT client WHERE id = clientId
  PG-->>CA: Client {income, creditScore, employmentStatus}

  CA->>PG: SELECT SUM(monthlyPayment) FROM credits<br/>WHERE clientId = ? AND status IN (Active, UnderReview)
  PG-->>CA: existingDebt

  CA->>CA: Calcula DTI proyectado<br/>= (existingDebt + newPayment) / income<br/>Rechaza si DTI > 60%

  CA->>CA: Credit.Create() → CreditCreatedDomainEvent

  rect rgb(240, 248, 255)
    Note over CA,PG: Transacción atómica (UnitOfWork)
    CA->>PG: INSERT INTO Credits (id, clientId, amount, status=Pending…)
    CA->>PG: INSERT INTO OutboxMessages (CreditCreatedIntegrationEvent)
    CA->>PG: COMMIT
  end

  CA->>Redis: SET idempotency:{key} = {creditDto} EX 86400
  CA-->>GW: 201 Created {creditId, status: "Pending"}
  GW-->>User: 201 Created {creditId}

  Note over CA: OutboxProcessorService (background)
  CA->>PG: SELECT * FROM OutboxMessages WHERE published = false
  CA->>MQ: Publish CreditCreatedIntegrationEvent<br/>{creditId, clientId, amount, interestRate, creditScore…}
  CA->>PG: UPDATE OutboxMessages SET published = true

  par Procesamiento paralelo en RabbitMQ
    MQ->>RA: CreditCreatedIntegrationEvent
    MQ->>AU: CreditCreatedIntegrationEvent
    MQ->>NO: CreditCreatedIntegrationEvent
  end

  Note over RA: Evaluación de Riesgo
  RA->>Redis: GET risk:processed:{eventId}
  Redis-->>RA: null (no procesado)

  RA->>RA: RiskCalculator.AssessAsync()<br/>newDti = newPayment/income × 100<br/>totalDti = (existing+new)/income × 100<br/>Decision: Approved/UnderReview/Rejected

  RA->>CA: POST /api/v1/credits/{creditId}/risk<br/>{riskScore, decision, reason}
  CA->>PG: UPDATE Credits SET riskScore=?, status=?
  CA->>PG: INSERT OutboxMessages (CreditUpdatedIntegrationEvent)
  CA-->>RA: 200 OK

  RA->>MQ: Publish RiskAssessedIntegrationEvent<br/>{creditId, riskScore, decision, reason}
  RA->>Redis: SET risk:processed:{eventId} EX 86400

  par Eventos de RiskAssessed
    MQ->>AU: RiskAssessedIntegrationEvent
    MQ->>NO: RiskAssessedIntegrationEvent
  end

  Note over AU: Registro de Auditoría
  AU->>AU: Verifica duplicado por EventId
  AU->>PG: INSERT INTO AuditRecords<br/>(CreditCreated + RiskAssessed)
  AU-->>MQ: ack

  Note over NO: Notificación al Usuario
  NO->>NO: ExistsForEventAsync(eventId)
  NO->>NO: Construye mensaje según Decision:<br/>Approved → "¡Crédito Aprobado!"<br/>Rejected → "Solicitud Rechazada"<br/>UnderReview → "En Revisión Manual"

  alt Envío exitoso
    NO->>NO: LogNotificationSender.SendAsync()
    NO->>PG: UPDATE status = Sent
    NO-->>MQ: ack
  else Fallo de envío
    NO->>MQ: Publish NotificationDeliveryFailedEvent<br/>{notificationId, attempt: 1}
    NO->>PG: UPDATE status = Failed

    loop Compensation (hasta MaxRetries=5)
      MQ->>NO: NotificationDeliveryFailedEvent
      NO->>NO: Reintenta envío
      alt Intento exitoso
        NO->>PG: UPDATE status = Sent
      else Máximo reintentos alcanzado
        NO->>MQ: Publish NotificationPermanentlyFailedEvent
        AU->>AU: Registra fallo permanente en auditoría
      end
    end
  end
```

## Resumen del flujo

| Paso | Servicio | Acción |
|---|---|---|
| 1 | **Gateway** | Valida JWT, aplica rate limit, proxea al Credits API |
| 2 | **Credits API** | Verifica idempotencia en Redis |
| 3 | **Credits API** | Carga cliente, calcula DTI (máx 60%) |
| 4 | **Credits API** | Crea crédito + OutboxMessage en transacción atómica |
| 5 | **Credits API** | Devuelve `201 Created` con `creditId` |
| 6 | **Credits API** | OutboxProcessor publica `CreditCreatedIntegrationEvent` |
| 7 | **Risk API** | Evalúa riesgo: calcula DTI real + credit score → Approved/UnderReview/Rejected |
| 8 | **Risk API** | Llama `POST /credits/{id}/risk` para actualizar estado |
| 9 | **Risk API** | Publica `RiskAssessedIntegrationEvent` |
| 10 | **Audit API** | Registra `CreditCreated` y `RiskAssessed` de forma inmutable |
| 11 | **Notifications API** | Envía notificación según decisión; reintenta hasta 5 veces si falla |

## Garantías del sistema

- **Idempotencia**: Repetir la misma request con el mismo `Idempotency-Key` devuelve el mismo resultado sin duplicados.
- **At-least-once delivery**: El Outbox Pattern garantiza que el evento se publica incluso si el servicio reinicia justo después del commit.
- **Deduplicación de eventos**: Risk, Audit y Notifications verifican el `EventId` antes de procesar para evitar efectos secundarios duplicados.
- **Compensación**: Si la notificación falla, el ciclo de compensación reintenta automáticamente hasta 5 veces antes de registrar el fallo permanente en auditoría.
