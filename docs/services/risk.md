# Risk API

**Puerto:** 5002 | **Sin base de datos propia** (lee de Credits API via HTTP)

## Responsabilidad

Evalúa automáticamente el riesgo crediticio de cada nueva solicitud. Consume el evento `CreditCreatedIntegrationEvent`, calcula un `riskScore` y una decisión (`Approved / UnderReview / Rejected`), informa al Credits API y publica `RiskAssessedIntegrationEvent`.

## Arquitectura interna

```
Kriteriom.Risk.Domain  → RiskAssessment, RiskCalculator, lógica de decisión
Kriteriom.Risk.API     → Consumer, Controller (health), Program.cs
```

## Algoritmo de evaluación (`RiskCalculator.AssessAsync`)

| Variable | Cálculo |
|---|---|
| `newDti` | `monthlyPayment / income × 100` |
| `totalDti` | `(existingDebt + monthlyPayment) / income × 100` |
| `riskScore` | Ponderación de `creditScore`, `totalDti` y `employmentStatus` |

### Tabla de decisión

| Condición | Decisión |
|---|---|
| `riskScore ≥ 700` y `totalDti ≤ 35 %` | `Approved` |
| `riskScore ≥ 600` y `totalDti ≤ 50 %` | `UnderReview` |
| Cualquier otro caso | `Rejected` |

## Flujo de procesamiento

```
RabbitMQ → CreditCreatedConsumer
  │  (1) Verifica deduplicación: GET risk:processed:{eventId} en Redis
  │      → Si existe: descarta (ack)
  │
  │  (2) RiskCalculator.AssessAsync(creditData)
  │      → Calcula newDti, totalDti, riskScore, decision, reason
  │
  │  (3) POST /api/v1/credits/{creditId}/risk → Credits API
  │      Body: { riskScore, decision, reason }
  │
  │  (4) Publish RiskAssessedIntegrationEvent → RabbitMQ
  │
  │  (5) SET risk:processed:{eventId} EX 86400 → Redis
  └──────────────────────────────────────────────────────
```

## Patrones técnicos

- **Idempotencia de eventos** — Redis `risk:processed:{eventId}` (TTL 24 h) evita doble evaluación
- **Circuit Breaker** — `Microsoft.Extensions.Http.Resilience` en la llamada HTTP a Credits API
- **At-least-once** — MassTransit reencola el mensaje si el consumer lanza excepción antes de ack

## Eventos consumidos

| Evento | Cola |
|---|---|
| `CreditCreatedIntegrationEvent` | `credit-created` |

## Eventos publicados

| Evento | Contenido |
|---|---|
| `RiskAssessedIntegrationEvent` | `creditId`, `riskScore`, `decision`, `reason`, `clientId` |
