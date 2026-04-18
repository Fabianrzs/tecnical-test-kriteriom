# Credits API

**Puerto:** 5001 | **Base de datos:** `credits_db` (PostgreSQL 16)

## Responsabilidad

Gestiona el ciclo de vida completo de créditos y clientes. Es el servicio central del sistema: recibe las solicitudes de crédito, calcula la capacidad de pago (DTI), persiste los datos y dispara todos los eventos de integración mediante el Outbox Pattern.

## Arquitectura interna

```
Kriteriom.Credits.Domain          → Entidades, Value Objects, Domain Events
Kriteriom.Credits.Application     → Commands, Queries, Handlers, Validators
Kriteriom.Credits.Infrastructure  → EF Core, Redis, MassTransit, Outbox
Kriteriom.Credits.API             → Controllers, Program.cs, appsettings
```

## Endpoints REST

### Créditos

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/v1/credits` | Crea un crédito — requiere `Idempotency-Key` en header |
| `GET` | `/api/v1/credits` | Lista créditos paginados (filtros: `clientId`, `status`, `page`, `size`) |
| `GET` | `/api/v1/credits/{id}` | Obtiene un crédito por ID |
| `PUT` | `/api/v1/credits/{id}/status` | Actualiza el estado de un crédito |
| `POST` | `/api/v1/credits/{id}/risk` | Aplica el resultado de evaluación de riesgo (llamado por Risk API) |
| `POST` | `/api/v1/credits/recalculate` | Fuerza recálculo de estados (llamado por Batch Processor) |
| `GET` | `/api/v1/credits/stats` | Estadísticas agregadas del portafolio |

### Clientes

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/v1/clients` | Crea un cliente |
| `GET` | `/api/v1/clients` | Lista clientes paginados |
| `GET` | `/api/v1/clients/{id}` | Obtiene un cliente por ID |
| `GET` | `/api/v1/clients/{id}/financial-summary` | DTI actual, deuda mensual, capacidad de pago |

## Reglas de negocio

| Regla | Descripción |
|---|---|
| **DTI máximo** | `(deuda_existente + nueva_cuota) / ingresos ≤ 60 %` — se rechaza si supera ese umbral |
| **Idempotencia** | `Idempotency-Key` (UUID v4) en header. Resultado cacheado en Redis 24 h. |
| **Estados de crédito** | `Pending → UnderReview → Approved / Rejected` · `Active → Expired` |
| **Reglas batch** | Créditos `Pending > 72 h` → `Expired`. Score alto + DTI bajo → `Approved` automático |

## Modelo de datos

```
Credits
  id (uuid PK)
  clientId (uuid FK)
  amount (decimal)
  interestRate (decimal)
  termMonths (int)
  monthlyPayment (decimal)
  status (enum: Pending/UnderReview/Approved/Rejected/Active/Expired)
  riskScore (int?)
  createdAt / updatedAt

Clients
  id (uuid PK)
  name / email
  income (decimal)
  creditScore (int)
  employmentStatus (enum)

OutboxMessages
  id (uuid PK)
  type (string)
  payload (jsonb)
  published (bool)
  publishedAt (timestamp?)

IdempotencyRecords
  key (string PK)
  response (jsonb)
  createdAt
  expiresAt
```

## Eventos publicados

| Evento | Disparado cuando |
|---|---|
| `CreditCreatedIntegrationEvent` | Se crea un crédito exitosamente |
| `CreditUpdatedIntegrationEvent` | Se actualiza el estado/riesgo de un crédito |

## Eventos consumidos

| Evento | Acción |
|---|---|
| `RecalculateCreditStatusesRequestedEvent` | Aplica reglas batch sobre todos los créditos activos |

## Patrones técnicos

- **CQRS** via MediatR — Commands y Queries separados
- **Pipeline behaviors** — Logging → Caching → Validation → Handler
- **Outbox Pattern** — garantía at-least-once en publicación de eventos
- **Unit of Work** — crédito + outbox en transacción atómica
- **Idempotencia** — Redis con TTL 24 h
- **Repository + Specification** — consultas tipadas y reutilizables
