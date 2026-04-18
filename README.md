# Kriteriom Credit Management System

Sistema de gestión de créditos construido con arquitectura de microservicios en .NET 9, diseñado para demostrar patrones avanzados como CQRS, Outbox Pattern, Event-Driven Architecture y observabilidad completa.

---

## Contenido del proyecto

### Servicios principales

| Servicio | Puerto | Descripción |
|---|---|---|
| **Credits API** | `5001` | Gestión de créditos — CQRS, Outbox Pattern, idempotencia |
| **Risk API** | `5002` | Evaluación de riesgo crediticio con circuit breaker |
| **Audit API** | `5003` | Registro inmutable de todos los eventos de integración |
| **Notifications API** | `5004` | Envío y compensación de notificaciones |
| **Batch Processor** | `5005` | Recálculo periódico de estados de crédito (Hangfire) |
| **API Gateway** | `8080` | Punto de entrada único — JWT, rate limiting, YARP reverse proxy |
| **Frontend** | `3001` | Interfaz React (Vite + TypeScript) |

### Infraestructura

| Componente | Puerto | Propósito |
|---|---|---|
| PostgreSQL 16 | `5432` | Base de datos principal (4 bases separadas por servicio) |
| RabbitMQ 3.13 | `5672` / `15672` | Message broker para eventos de integración |
| Redis 7.2 | `6379` | Caché distribuido e idempotencia |
| HashiCorp Vault 1.15 | `8200` | Gestión centralizada de secretos |
| Prometheus | `9090` | Recolección de métricas |
| Grafana | `3000` | Dashboards de observabilidad |
| Jaeger | `16686` | Trazabilidad distribuida (OTLP) |

### Arquitectura del SharedKernel

El SharedKernel está dividido en 4 capas independientes para evitar dependencias innecesarias:

- **`SharedKernel.Domain`** — Primitivos de dominio: `Entity`, `AggregateRoot`, `ValueObject`, `Result`
- **`SharedKernel.Contracts`** — Contratos compartidos: eventos de integración, interfaces de Outbox
- **`SharedKernel.Application`** — Comportamientos CQRS: `LoggingBehavior`, `CachingBehavior`, `ValidationBehavior`
- **`SharedKernel.Infrastructure`** — Extensiones reutilizables: Serilog, OpenTelemetry, RabbitMQ, Swagger, Redis, EF Core

---

## Requisitos previos

- [Docker](https://docs.docker.com/get-docker/) y Docker Compose v2
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (solo para desarrollo local)
- [Node.js 20+](https://nodejs.org/) (solo para desarrollo local del frontend)

---

## Cómo correr el proyecto

### 1. Crear el archivo de variables de entorno

Crea un archivo `.env` en la raíz del proyecto con el siguiente contenido:

```env
# PostgreSQL
POSTGRES_USER=admin
POSTGRES_PASSWORD=admin123

# RabbitMQ
RABBITMQ_USER=admin
RABBITMQ_PASS=admin123

# Redis
REDIS_PASSWORD=redis123

# Vault
VAULT_DEV_ROOT_TOKEN_ID=root-token

# Grafana
GRAFANA_ADMIN_PASSWORD=admin123
```

### 2. Levantar todos los servicios

```bash
docker compose up --build
```

> La primera vez tarda algunos minutos porque construye las imágenes y aplica las migraciones de base de datos automáticamente.

### 3. Verificar que todo está corriendo

```bash
docker compose ps
```

---

## URLs de acceso

| Recurso | URL |
|---|---|
| Frontend | http://localhost:3000 |
| API Gateway | http://localhost:8080 |
| Credits API — Swagger | http://localhost:5001/swagger |
| Audit API — Swagger | http://localhost:5003/swagger |
| Batch Processor — Swagger | http://localhost:5005/swagger |
| Batch Processor — Hangfire | http://localhost:5005/hangfire |
| RabbitMQ Management | http://localhost:15672 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Jaeger UI | http://localhost:16686 |
| Vault UI | http://localhost:8200 |

### Autenticación en el Gateway

El gateway expone un endpoint de tokens JWT:

```bash
POST http://localhost:8080/auth/token
Content-Type: application/json

{ "username": "admin", "password": "admin123" }
```

---

## Estructura del repositorio

```
.
├── src/
│   ├── SharedKernel/
│   │   ├── Kriteriom.SharedKernel.Domain/        # Primitivos de dominio
│   │   ├── Kriteriom.SharedKernel.Contracts/     # Eventos de integración y Outbox
│   │   ├── Kriteriom.SharedKernel.Application/   # Comportamientos CQRS
│   │   └── Kriteriom.SharedKernel.Infrastructure/# Extensiones de infraestructura
│   ├── Services/
│   │   ├── Credits/       # Dominio, Aplicación, Infraestructura y API de créditos
│   │   ├── Risk/          # Evaluación de riesgo
│   │   ├── Audit/         # Auditoría de eventos
│   │   └── Notifications/ # Notificaciones con compensación
│   ├── Services/BatchProcessor/  # Procesamiento batch con Hangfire
│   └── Gateway/                  # API Gateway con YARP
├── frontend/                     # React + Vite + TypeScript
├── tests/                        # Tests unitarios con xUnit
├── infra/                        # Configuración de infraestructura local
│   ├── postgres/                 # Script de inicialización de bases de datos
│   ├── rabbitmq/                 # Configuración del broker
│   ├── vault/                    # Script de inicialización de secretos
│   ├── prometheus/               # Configuración de scraping
│   └── grafana/                  # Dashboards y provisioning
├── docs/                         # Documentación adicional y ADRs
├── docker-compose.yml
└── Directory.Packages.props      # Versiones centralizadas de paquetes NuGet
```

---

## Stack tecnológico

| Categoría | Tecnología |
|---|---|
| Backend | .NET 9, ASP.NET Core, Entity Framework Core 9 |
| Mensajería | MassTransit 8, RabbitMQ |
| Base de datos | PostgreSQL 16, Npgsql |
| Caché | Redis 7, StackExchange.Redis |
| Resiliencia | Polly 8, Microsoft.Extensions.Http.Resilience |
| Observabilidad | OpenTelemetry, Prometheus, Grafana, Jaeger (OTLP) |
| Logging | Serilog |
| Secretos | HashiCorp Vault |
| Gateway | YARP Reverse Proxy, JWT Bearer |
| Jobs | Hangfire + PostgreSQL storage |
| Frontend | React 19, TypeScript, Vite |
| Tests | xUnit, FluentAssertions, NSubstitute |

---

## Comandos útiles

```bash
# Detener todos los servicios
docker compose down

# Detener y eliminar volúmenes (borra datos)
docker compose down -v

# Ver logs de un servicio específico
docker compose logs -f credits-api

# Reconstruir un servicio sin afectar los demás
docker compose up --build credits-api

# Ejecutar tests
dotnet test CreditManagement.sln
```
