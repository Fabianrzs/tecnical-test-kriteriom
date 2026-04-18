workspace "Kriteriom Credit Management" "Sistema de gestión del ciclo de vida de créditos con evaluación de riesgo automatizada, auditoría y notificaciones." {

    model {

        borrower  = person "Solicitante de Crédito" "Persona que solicita y consulta el estado de sus créditos."
        analyst   = person "Analista de Crédito"    "Revisa evaluaciones de riesgo y el estado de los créditos."
        admin     = person "Administrador"           "Gestiona el sistema, visualiza métricas y ejecuta jobs batch."

        vault               = softwareSystem "HashiCorp Vault"          "Gestión centralizada de secretos y credenciales."          "External"
        monitoring          = softwareSystem "Prometheus + Grafana"     "Recolección de métricas y dashboards de observabilidad."   "External"
        tracing             = softwareSystem "Jaeger"                   "Trazabilidad distribuida de requests entre microservicios." "External"
        notificationChannel = softwareSystem "Canal de Notificaciones"  "Email / SMS (simulado con logging estructurado)."          "External"

        kriteriom = softwareSystem "Kriteriom Credit Management" "Gestión completa del ciclo de vida de créditos con evaluación de riesgo automatizada, auditoría y notificaciones." {

            frontend = container "Frontend" "Interfaz de usuario para gestión de créditos. SPA que consume el API Gateway." "React 19 + Vite + TypeScript" "Web Browser"

            gateway = container "API Gateway" "Punto de entrada único: autenticación JWT, rate limiting y reverse proxy." ".NET 9 / YARP" "API" {
                gwVaultLoader    = component "VaultSecretLoader"       "Carga el JWT secret y demás configuraciones desde HashiCorp Vault al arrancar."                                                                                                                            "VaultSharp / IHostedService"
                gwJwtMiddleware  = component "JwtAuthMiddleware"       "Valida la firma, issuer, audience y expiración del Bearer token en cada request."                                                                                                                         "ASP.NET Core Authentication / JwtBearer"
                gwRateLimit      = component "RateLimitMiddleware"     "Aplica límite fijo de 100 req/min por IP. Devuelve 429 con header Retry-After si se supera."                                                                                                             "ASP.NET Core Rate Limiting"
                gwCorrelation    = component "CorrelationIdMiddleware"  "Genera o propaga el header X-Correlation-Id hacia los servicios downstream."                                                                                                                             "ASP.NET Core Middleware"
                gwProxy          = component "ReverseProxy"            "Enruta /api/credits y /api/clients → Credits API :5001, /api/audit → Audit API :5003, /api/batch y /hangfire → Batch Processor :5005."                                                                  "YARP"
                gwHealthCtrl     = component "HealthController"        "Endpoint GET /health que responde sin autenticación. Usado por Docker y load balancers."                                                                                                                  "ASP.NET Core Controller"
                gwTelemetry      = component "TelemetryPipeline"       "Exporta trazas OTLP a Jaeger y expone /metrics para Prometheus."                                                                                                                                         "OpenTelemetry SDK"
            }

            creditsApi = container "Credits API" "Gestión del ciclo de vida de créditos y clientes. CQRS + Outbox Pattern." ".NET 9 / ASP.NET Core" "API" {
                creditsCtrl     = component "CreditsController"                       "Endpoints REST: POST /credits, GET /credits, GET /credits/{id}, PUT /credits/{id}/status, POST /credits/{id}/risk, POST /credits/recalculate, GET /credits/stats."                        "ASP.NET Core Controller"
                clientsCtrl     = component "ClientsController"                       "Endpoints REST: POST /clients, GET /clients, GET /clients/{id}, GET /clients/{id}/financial-summary."                                                                                     "ASP.NET Core Controller"
                mediatrPipeline = component "MediatR Pipeline"                        "Cadena de behaviors: LoggingBehavior → CachingBehavior → ValidationBehavior → Handler. Aplica cross-cutting concerns de forma declarativa."                                               "MediatR + IPipelineBehavior"
                createCmd       = component "CreateCreditCommandHandler"              "Valida DTI proyectado (máx 60%), verifica idempotencia en Redis, crea la entidad Credit, persiste vía UnitOfWork y almacena CreditCreatedIntegrationEvent en el Outbox."                  "MediatR ICommandHandler"
                riskCmd         = component "ProcessRiskAssessmentCommandHandler"     "Recibe el resultado de evaluación de riesgo (score, decision, reason), actualiza el estado del crédito y actualiza el creditScore del cliente."                                            "MediatR ICommandHandler"
                statusCmd       = component "UpdateCreditStatusCommandHandler"        "Valida que la transición de estado sea permitida (máquina de estados) y persiste el nuevo estado con su motivo."                                                                          "MediatR ICommandHandler"
                recalcCmd       = component "RecalculateCreditStatusesCommandHandler" "Carga todos los créditos en estado Pending o UnderReview y aplica reglas de negocio: timeout → Expired, score alto → Approved, score bajo → Rejected."                                   "MediatR ICommandHandler"
                getCreditQ      = component "GetCreditQueryHandler"                   "Retorna un crédito por ID con todos sus campos."                                                                                                                                           "MediatR IQueryHandler"
                getCreditsQ     = component "GetCreditsQueryHandler"                  "Consulta paginada de créditos con filtros opcionales por clientId, status, fecha."                                                                                                        "MediatR IQueryHandler"
                statsQ          = component "GetCreditStatsQueryHandler"              "Calcula estadísticas agregadas del portafolio: total, aprobados, rechazados, tasa de aprobación, monto promedio."                                                                         "MediatR IQueryHandler"
                financialQ      = component "GetClientFinancialSummaryHandler"        "Calcula el DTI actual del cliente sumando cuotas mensuales de créditos activos y comparándolas con sus ingresos declarados."                                                              "MediatR IQueryHandler"
                recalcConsumer  = component "RecalculateCreditStatusesConsumer"       "Escucha RecalculateCreditStatusesRequestedEvent publicado por Batch Processor y reenvía al RecalculateCreditStatusesCommandHandler."                                                      "MassTransit IConsumer"
                creditRepo      = component "CreditRepository"                        "CRUD de créditos usando especificaciones de dominio (ISpecification<Credit>) para consultas tipadas y reusables."                                                                         "EF Core / Repository + Specification Pattern"
                clientRepo      = component "ClientRepository"                        "CRUD de clientes. Expone GetByIdAsync y UpdateCreditScoreAsync."                                                                                                                          "EF Core / Repository Pattern"
                outboxRepo      = component "OutboxRepository"                        "Escribe OutboxMessage y lee pendientes (published = false). Usado por UnitOfWork y OutboxProcessorService."                                                                                "EF Core"
                idempotencySvc  = component "IdempotencyService"                      "Verifica si una Idempotency-Key ya fue procesada (GET) y almacena el resultado serializado (SET EX 86400) en Redis."                                                                      "StackExchange.Redis"
                uow             = component "UnitOfWork"                              "Abre una transacción EF Core y confirma CreditRepository + OutboxRepository en un único COMMIT atómico."                                                                                  "EF Core DbContext Transaction"
                outboxWorker    = component "OutboxProcessorService"                  "Worker en background que cada 5 s lee OutboxMessages pendientes, los publica en RabbitMQ vía MassTransit y los marca como published = true."                                              "IHostedService / MassTransit"
                dbCtx           = component "CreditsDbContext"                        "DbContext de EF Core que mapea: Credits, Clients, OutboxMessages, IdempotencyRecords a la base credits_db."                                                                                "EF Core DbContext"
            }

            riskApi = container "Risk API" "Evaluación automática de riesgo crediticio. Sin base de datos propia; lee del evento y escribe en Credits API." ".NET 9 / ASP.NET Core" "API" {
                riskVaultLoader       = component "VaultSecretLoader"        "Carga credenciales de RabbitMQ, Redis y la URL de Credits API desde Vault al iniciar."                                                                                                             "VaultSharp / IHostedService"
                creditCreatedConsumer = component "CreditCreatedConsumer"    "Escucha CreditCreatedIntegrationEvent desde RabbitMQ. Orquesta la evaluación y delega en los componentes internos."                                                                                "MassTransit IConsumer"
                riskIdempotency       = component "RiskIdempotencyGuard"     "Antes de evaluar, consulta en Redis la clave risk:processed:{eventId}. Si existe, hace ack sin procesar (deduplicación)."                                                                          "StackExchange.Redis"
                riskCalculator        = component "RiskCalculator"           "Calcula newDti = cuota/ingresos, totalDti = (deudaExistente+cuota)/ingresos y riskScore ponderando creditScore, totalDti y employmentStatus. Retorna RiskAssessment con decision y reason."        "Domain Service"
                riskAssessmentEntity  = component "RiskAssessment"           "Entidad de dominio que encapsula el resultado: riskScore, decision, newDti, totalDti, reason, assessedAt."                                                                                         "Domain Entity"
                creditsHttpClient     = component "CreditsApiClient"         "Cliente HTTP con circuit breaker (Polly) y retry policy. Llama POST /api/v1/credits/{creditId}/risk con el resultado de la evaluación."                                                            "HttpClient + Microsoft.Extensions.Http.Resilience"
                riskAssessedPublisher = component "RiskAssessedPublisher"    "Publica RiskAssessedIntegrationEvent {creditId, clientId, riskScore, decision, reason} en RabbitMQ para que Audit y Notifications lo consuman."                                                    "MassTransit IPublishEndpoint"
                riskRedisWriter       = component "RiskProcessedMarker"      "Una vez publicado el evento, escribe risk:processed:{eventId} en Redis con TTL 24 h para garantizar idempotencia."                                                                                 "StackExchange.Redis"
                riskTelemetry         = component "TelemetryPipeline"        "Exporta trazas OTLP a Jaeger y expone /metrics para Prometheus."                                                                                                                                   "OpenTelemetry SDK"
            }

            auditApi = container "Audit API" "Registro inmutable de todos los eventos del sistema. Nunca actualiza ni elimina registros." ".NET 9 / ASP.NET Core" "API" {
                auditVaultLoader    = component "VaultSecretLoader"              "Carga las credenciales de PostgreSQL y RabbitMQ desde Vault al arrancar."                                                                                                                       "VaultSharp / IHostedService"
                auditController     = component "AuditController"                "Endpoints REST de solo lectura: GET /api/v1/audit (paginado, filtros por entityId, eventType, rango de fechas) y GET /api/v1/audit/{id}."                                                      "ASP.NET Core Controller"
                auditQueryHandler   = component "GetAuditRecordsQueryHandler"    "Construye la consulta paginada sobre AuditRecords aplicando los filtros recibidos del controlador."                                                                                             "MediatR IQueryHandler"
                duplicateGuard      = component "EventDuplicateGuard"            "Verifica SELECT 1 FROM AuditRecords WHERE eventId = ? antes de insertar. Si ya existe, hace ack sin persistir (idempotencia)."                                                                 "EF Core"
                creditCreatedAudit  = component "CreditCreatedAuditConsumer"     "Consume CreditCreatedIntegrationEvent y crea un AuditRecord con snapshot completo del evento."                                                                                                 "MassTransit IConsumer"
                creditUpdatedAudit  = component "CreditUpdatedAuditConsumer"     "Consume CreditUpdatedIntegrationEvent y registra el cambio de estado del crédito."                                                                                                             "MassTransit IConsumer"
                riskAssessedAudit   = component "RiskAssessedAuditConsumer"      "Consume RiskAssessedIntegrationEvent y registra el resultado de la evaluación de riesgo."                                                                                                      "MassTransit IConsumer"
                notifFailedAudit    = component "NotificationFailedAuditConsumer" "Consume NotificationDeliveryFailedEvent y NotificationPermanentlyFailedEvent y los registra con nivel de alerta."                                                                             "MassTransit IConsumer"
                auditRepository     = component "AuditRepository"               "Solo INSERT. Recibe un AuditRecord y lo persiste en audit_db. Nunca expone UPDATE ni DELETE."                                                                                                   "EF Core / Repository Pattern"
                auditDbCtx          = component "AuditDbContext"                 "DbContext de EF Core que mapea la tabla AuditRecords a audit_db."                                                                                                                               "EF Core DbContext"
                auditTelemetry      = component "TelemetryPipeline"              "Exporta trazas OTLP a Jaeger y expone /metrics para Prometheus."                                                                                                                                "OpenTelemetry SDK"
            }

            notificationsApi = container "Notifications API" "Envío de notificaciones al usuario con saga de compensación (hasta 5 reintentos automáticos)." ".NET 9 / ASP.NET Core" "API" {
                notifVaultLoader          = component "VaultSecretLoader"                "Carga las credenciales de PostgreSQL y RabbitMQ desde Vault al arrancar."                                                                                                              "VaultSharp / IHostedService"
                riskAssessedNotifConsumer = component "RiskAssessedNotificationConsumer" "Punto de entrada principal. Consume RiskAssessedIntegrationEvent, verifica idempotencia, construye la notificación y delega el envío."                                                 "MassTransit IConsumer"
                notifDeliveryConsumer     = component "NotificationDeliveryFailedConsumer" "Consume NotificationDeliveryFailedEvent para ejecutar reintentos. Si attempt ≥ MaxRetries (5), publica NotificationPermanentlyFailedEvent."                                          "MassTransit IConsumer"
                notifIdempotency          = component "NotificationIdempotencyGuard"    "Consulta en notifications_db si ya existe un registro para el eventId recibido. Evita crear notificaciones duplicadas."                                                                 "EF Core"
                notifBuilder              = component "NotificationMessageBuilder"       "Construye el asunto y cuerpo del mensaje según la decision: Approved → ¡Crédito Aprobado!, Rejected → Solicitud Rechazada, UnderReview → En Revisión Manual."                          "Domain Service"
                notifSender               = component "LogNotificationSender"            "Implementación actual de INotificationSender. Escribe la notificación como log estructurado (Serilog). Reemplazable por SMTP o SMS sin cambiar la lógica."                             "INotificationSender / Serilog"
                compensationPublisher     = component "CompensationEventPublisher"       "Publica NotificationDeliveryFailedEvent {notificationId, attempt} para disparar el siguiente reintento, o NotificationPermanentlyFailedEvent cuando se agota MaxRetries."              "MassTransit IPublishEndpoint"
                notifRepository           = component "NotificationRepository"           "CRUD de notificaciones: INSERT al crear, UPDATE status y attempts en cada intento de envío."                                                                                            "EF Core / Repository Pattern"
                notifDbCtx                = component "NotificationsDbContext"           "DbContext de EF Core que mapea la tabla Notifications a notifications_db."                                                                                                              "EF Core DbContext"
                notifTelemetry            = component "TelemetryPipeline"                "Exporta trazas OTLP a Jaeger y expone /metrics para Prometheus."                                                                                                                       "OpenTelemetry SDK"
            }

            batchProcessor = container "Batch Processor" "Ejecuta jobs periódicos para mantener la consistencia del portafolio de créditos." ".NET 9 / Hangfire" "Service" {
                batchVaultLoader    = component "VaultSecretLoader"             "Carga credenciales de PostgreSQL, RabbitMQ y la URL de Credits API desde Vault al arrancar."                                                                                                    "VaultSharp / IHostedService"
                batchController     = component "BatchController"               "Endpoints REST: POST /api/v1/batch/recalculate (disparo manual) y GET /api/v1/batch/jobs (historial de ejecuciones)."                                                                           "ASP.NET Core Controller"
                hangfireScheduler   = component "HangfireScheduler"             "Registra y programa los jobs recurrentes: RecalculateCreditStatusesJob (cron cada 6 h) y CleanOutboxJob (cron diario 03:00 UTC)."                                                               "Hangfire RecurringJob"
                hangfireDashboard   = component "HangfireDashboard"             "Dashboard web en /hangfire con historial de jobs, reintentos y métricas de ejecución. Acceso solo con rol Admin."                                                                               "Hangfire.AspNetCore"
                recalcJob           = component "RecalculateCreditStatusesJob"  "Consulta todos los créditos Pending y UnderReview, aplica reglas (timeout → Expired, score alto → Approved, score bajo → Rejected), llama Credits API para actualizar y publica el evento."    "Hangfire IJob"
                cleanOutboxJob      = component "CleanOutboxJob"                "Elimina OutboxMessages con published = true y publishedAt de hace más de 7 días para evitar crecimiento ilimitado de la tabla."                                                                 "Hangfire IJob"
                batchCreditsClient  = component "CreditsApiClient"              "Cliente HTTP con circuit breaker y retry policy. Llama GET /api/v1/credits para obtener créditos y PUT /api/v1/credits/{id}/status para actualizar estados."                                    "HttpClient + Microsoft.Extensions.Http.Resilience"
                recalcPublisher     = component "RecalculateEventPublisher"     "Publica RecalculateCreditStatusesRequestedEvent {jobId, processedCount, updatedCount} en RabbitMQ al finalizar cada ejecución del job."                                                         "MassTransit IPublishEndpoint"
                batchJobRepository  = component "JobExecutionRepository"        "Registra el resultado de cada ejecución de job: jobId, tipo, inicio, fin, registros procesados, registros actualizados, errores."                                                               "EF Core / Repository Pattern"
                batchDbCtx          = component "BatchDbContext"                "DbContext de EF Core que mapea la tabla JobExecutions y los metadatos de Hangfire a batch_db."                                                                                                  "EF Core DbContext + Hangfire PostgreSQL"
                batchTelemetry      = component "TelemetryPipeline"             "Exporta trazas OTLP a Jaeger y expone /metrics para Prometheus."                                                                                                                                "OpenTelemetry SDK"
            }

            creditsDb       = container "credits_db"       "Créditos, clientes, outbox messages, idempotency records."  "PostgreSQL 16" "Database"
            auditDb         = container "audit_db"         "Registros de auditoría inmutables."                          "PostgreSQL 16" "Database"
            notificationsDb = container "notifications_db" "Historial de notificaciones enviadas."                       "PostgreSQL 16" "Database"
            batchDb         = container "batch_db"         "Checkpoints y logs de jobs batch (Hangfire)."               "PostgreSQL 16" "Database"
            rabbitmq        = container "RabbitMQ 3.13"    "Bus de eventos de integración entre microservicios."         "RabbitMQ"      "Message Broker"
            redis           = container "Redis 7.2"        "Caché distribuido e idempotencia."                           "Redis"         "Cache"
        }

        borrower  -> kriteriom           "Solicita créditos, consulta estado"              "HTTPS"
        analyst   -> kriteriom           "Revisa créditos, consulta auditoría"             "HTTPS"
        admin     -> kriteriom           "Gestiona sistema, ejecuta batch jobs"            "HTTPS"
        kriteriom -> vault               "Obtiene secretos al arrancar"                    "HTTP :8200"
        kriteriom -> monitoring          "Expone métricas Prometheus"                      "HTTP /metrics"
        kriteriom -> tracing             "Envía trazas OTLP"                               "gRPC :4317"
        kriteriom -> notificationChannel "Envía notificaciones"                            "Interno (log)"

        borrower  -> frontend      "Usa"                                                   "HTTPS :3001"
        analyst   -> gateway       "Llama directamente"                                    "HTTP :8080"
        admin     -> gateway       "Llama directamente"                                    "HTTP :8080"
        frontend  -> gateway       "Todas las llamadas REST"                               "HTTP :8080"

        gateway          -> creditsApi       "Proxea /api/credits, /api/clients"           "HTTP :5001"
        gateway          -> auditApi         "Proxea /api/audit"                           "HTTP :5003"
        gateway          -> batchProcessor   "Proxea /api/batch, /hangfire"               "HTTP :5005"

        creditsApi       -> creditsDb        "Lee y escribe"                               "TCP :5432 / EF Core"
        creditsApi       -> rabbitmq         "Publica eventos via Outbox"                  "AMQP :5672 / MassTransit"
        creditsApi       -> redis            "Idempotencia y caché de queries"             "TCP :6379"
        creditsApi       -> vault            "Carga secrets al iniciar"                    "HTTP :8200"
        creditsApi       -> tracing          "Envía trazas OTLP"                           "gRPC :4317"
        creditsApi       -> monitoring       "Expone /metrics"                             "HTTP"

        riskApi          -> rabbitmq         "Consume CreditCreated; publica RiskAssessed" "AMQP :5672 / MassTransit"
        riskApi          -> creditsApi       "POST /api/v1/credits/{id}/risk"              "HTTP :5001"
        riskApi          -> redis            "Deduplicación de eventos procesados"         "TCP :6379"
        riskApi          -> vault            "Carga secrets al iniciar"                    "HTTP :8200"
        riskApi          -> tracing          "Envía trazas OTLP"                           "gRPC :4317"
        riskApi          -> monitoring       "Expone /metrics"                             "HTTP"

        auditApi         -> rabbitmq         "Consume todos los eventos"                   "AMQP :5672 / MassTransit"
        auditApi         -> auditDb          "Escribe registros inmutables"                "TCP :5432 / EF Core"
        auditApi         -> vault            "Carga secrets al iniciar"                    "HTTP :8200"
        auditApi         -> tracing          "Envía trazas OTLP"                           "gRPC :4317"
        auditApi         -> monitoring       "Expone /metrics"                             "HTTP"

        notificationsApi -> rabbitmq         "Consume y publica eventos de notificación"   "AMQP :5672 / MassTransit"
        notificationsApi -> notificationsDb  "Lee y escribe historial"                     "TCP :5432 / EF Core"
        notificationsApi -> vault            "Carga secrets al iniciar"                    "HTTP :8200"
        notificationsApi -> tracing          "Envía trazas OTLP"                           "gRPC :4317"
        notificationsApi -> monitoring       "Expone /metrics"                             "HTTP"
        notificationsApi -> notificationChannel "Envía notificaciones (log)"               "Interno"

        batchProcessor   -> batchDb          "Checkpoints y logs Hangfire"                 "TCP :5432 / EF Core + Hangfire"
        batchProcessor   -> rabbitmq         "Publica RecalculateRequested"                "AMQP :5672 / MassTransit"
        batchProcessor   -> creditsApi       "GET /api/credits · PUT /api/credits/{id}/status" "HTTP :5001"
        batchProcessor   -> vault            "Carga secrets al iniciar"                    "HTTP :8200"
        batchProcessor   -> tracing          "Envía trazas OTLP"                           "gRPC :4317"
        batchProcessor   -> monitoring       "Expone /metrics"                             "HTTP"

        gwVaultLoader   -> vault            "Carga JWT secret y configuración"             "HTTP :8200"
        gwJwtMiddleware -> gwVaultLoader    "Obtiene la clave de firma JWT"
        gwCorrelation   -> gwJwtMiddleware  "Se ejecuta después de autenticación"
        gwRateLimit     -> gwCorrelation    "Se ejecuta después de correlación"
        gwProxy         -> gwRateLimit      "Se ejecuta si el request pasa el rate limit"
        gwProxy         -> creditsApi       "Proxea /api/credits, /api/clients"            "HTTP :5001"
        gwProxy         -> auditApi         "Proxea /api/audit"                            "HTTP :5003"
        gwProxy         -> batchProcessor   "Proxea /api/batch, /hangfire"                "HTTP :5005"
        gwTelemetry     -> tracing          "Envía trazas"                                 "gRPC :4317"
        gwTelemetry     -> monitoring       "Expone /metrics"                              "HTTP"

        creditsCtrl     -> mediatrPipeline  "Envía commands y queries"
        clientsCtrl     -> mediatrPipeline  "Envía commands y queries"
        mediatrPipeline -> createCmd        "Despacha CreateCreditCommand"
        mediatrPipeline -> riskCmd          "Despacha ProcessRiskAssessmentCommand"
        mediatrPipeline -> statusCmd        "Despacha UpdateCreditStatusCommand"
        mediatrPipeline -> recalcCmd        "Despacha RecalculateCreditStatusesCommand"
        mediatrPipeline -> getCreditQ       "Despacha GetCreditQuery"
        mediatrPipeline -> getCreditsQ      "Despacha GetCreditsQuery"
        mediatrPipeline -> statsQ           "Despacha GetCreditStatsQuery"
        mediatrPipeline -> financialQ       "Despacha GetClientFinancialSummaryQuery"
        recalcConsumer  -> mediatrPipeline  "Envía RecalculateCreditStatusesCommand"
        recalcConsumer  -> rabbitmq         "Escucha RecalculateCreditStatusesRequestedEvent" "MassTransit AMQP"
        createCmd       -> idempotencySvc   "CheckAsync(idempotencyKey)"
        createCmd       -> clientRepo       "GetByIdAsync(clientId)"
        createCmd       -> creditRepo       "GetActiveDebtAsync(clientId)"
        createCmd       -> uow              "Persiste crédito + OutboxMessage atómicamente"
        createCmd       -> idempotencySvc   "SetAsync(key, result, TTL 24 h)"
        uow             -> creditRepo       "AddAsync(credit)"
        uow             -> outboxRepo       "AddAsync(outboxMessage)"
        uow             -> dbCtx            "SaveChangesAsync() — COMMIT atómico"
        riskCmd         -> creditRepo       "GetByIdAsync(creditId)"
        riskCmd         -> creditRepo       "UpdateAsync(credit)"
        riskCmd         -> clientRepo       "UpdateCreditScoreAsync(clientId, newScore)"
        riskCmd         -> outboxRepo       "AddAsync(CreditUpdatedIntegrationEvent)"
        statusCmd       -> creditRepo       "GetByIdAsync — valida transición de estado"
        statusCmd       -> creditRepo       "UpdateAsync — persiste nuevo estado"
        recalcCmd       -> creditRepo       "GetPendingAndUnderReviewAsync()"
        recalcCmd       -> creditRepo       "UpdateAsync (×N)"
        getCreditQ      -> creditRepo       "GetByIdAsync(creditId)"
        getCreditsQ     -> creditRepo       "GetPagedAsync(filters, page, size)"
        statsQ          -> creditRepo       "GetAggregatedStatsAsync()"
        financialQ      -> creditRepo       "GetActiveMonthlyDebtAsync(clientId)"
        financialQ      -> clientRepo       "GetByIdAsync(clientId)"
        outboxWorker    -> outboxRepo       "GetPendingAsync()"
        outboxWorker    -> rabbitmq         "Publica integrationEvents"                    "MassTransit"
        outboxWorker    -> outboxRepo       "MarkAsPublishedAsync(ids)"
        creditRepo      -> dbCtx            "Usa CreditsDbContext"
        clientRepo      -> dbCtx            "Usa CreditsDbContext"
        outboxRepo      -> dbCtx            "Usa CreditsDbContext"
        idempotencySvc  -> redis            "GET / SET con TTL"                            "StackExchange.Redis"
        dbCtx           -> creditsDb        "Lee y escribe"                                "TCP :5432"

        riskVaultLoader       -> vault               "Carga credenciales RabbitMQ, Redis, Credits API URL" "HTTP :8200"
        creditCreatedConsumer -> riskIdempotency      "Verifica risk:processed:{eventId}"
        riskIdempotency       -> redis               "GET risk:processed:{eventId}"                        "StackExchange.Redis"
        creditCreatedConsumer -> riskCalculator       "AssessAsync(creditData)"
        riskCalculator        -> riskAssessmentEntity "Crea RiskAssessment con score, decision, reason"
        creditCreatedConsumer -> creditsHttpClient    "POST /api/v1/credits/{creditId}/risk"
        creditsHttpClient     -> creditsApi           "HTTP con circuit breaker"                            "HTTP :5001"
        creditCreatedConsumer -> riskAssessedPublisher "Publish RiskAssessedIntegrationEvent"
        riskAssessedPublisher -> rabbitmq             "RiskAssessedIntegrationEvent"                        "MassTransit AMQP"
        creditCreatedConsumer -> riskRedisWriter       "SET risk:processed:{eventId} EX 86400"
        riskRedisWriter       -> redis               "SET con TTL 24 h"                                     "StackExchange.Redis"
        creditCreatedConsumer -> rabbitmq             "Escucha CreditCreatedIntegrationEvent"                "MassTransit AMQP"
        riskTelemetry         -> tracing             "Envía trazas"                                         "gRPC :4317"
        riskTelemetry         -> monitoring          "Expone /metrics"                                      "HTTP"

        auditVaultLoader    -> vault               "Carga credenciales PostgreSQL y RabbitMQ"              "HTTP :8200"
        auditController     -> auditQueryHandler   "Envía GetAuditRecordsQuery"
        auditQueryHandler   -> auditRepository     "GetPagedAsync(filters, page, size)"
        creditCreatedAudit  -> duplicateGuard      "ExistsAsync(eventId)"
        creditUpdatedAudit  -> duplicateGuard      "ExistsAsync(eventId)"
        riskAssessedAudit   -> duplicateGuard      "ExistsAsync(eventId)"
        notifFailedAudit    -> duplicateGuard      "ExistsAsync(eventId)"
        duplicateGuard      -> auditRepository     "SELECT 1 WHERE eventId = ?"
        creditCreatedAudit  -> auditRepository     "InsertAsync(AuditRecord{type=CreditCreated})"
        creditUpdatedAudit  -> auditRepository     "InsertAsync(AuditRecord{type=CreditUpdated})"
        riskAssessedAudit   -> auditRepository     "InsertAsync(AuditRecord{type=RiskAssessed})"
        notifFailedAudit    -> auditRepository     "InsertAsync(AuditRecord{type=NotificationFailed})"
        auditRepository     -> auditDbCtx          "Usa AuditDbContext"
        auditDbCtx          -> auditDb             "Lee y escribe"                                          "TCP :5432"
        creditCreatedAudit  -> rabbitmq            "Escucha CreditCreatedIntegrationEvent"                  "MassTransit AMQP"
        creditUpdatedAudit  -> rabbitmq            "Escucha CreditUpdatedIntegrationEvent"                  "MassTransit AMQP"
        riskAssessedAudit   -> rabbitmq            "Escucha RiskAssessedIntegrationEvent"                   "MassTransit AMQP"
        notifFailedAudit    -> rabbitmq            "Escucha NotificationDelivery/PermanentlyFailedEvent"     "MassTransit AMQP"
        auditTelemetry      -> tracing             "Envía trazas"                                            "gRPC :4317"
        auditTelemetry      -> monitoring          "Expone /metrics"                                         "HTTP"

        notifVaultLoader           -> vault               "Carga credenciales PostgreSQL y RabbitMQ"        "HTTP :8200"
        riskAssessedNotifConsumer  -> notifIdempotency    "ExistsForEventAsync(eventId)"
        riskAssessedNotifConsumer  -> notifBuilder        "Build(decision, creditId, clientId)"
        riskAssessedNotifConsumer  -> notifRepository     "InsertAsync(Notification{status=Pending})"
        riskAssessedNotifConsumer  -> notifSender         "SendAsync(message)"
        notifSender                -> notificationChannel "Escribe log estructurado"
        riskAssessedNotifConsumer  -> notifRepository     "UpdateAsync(status=Sent)"
        riskAssessedNotifConsumer  -> compensationPublisher "Publish NotificationDeliveryFailedEvent{attempt:1}"
        riskAssessedNotifConsumer  -> notifRepository     "UpdateAsync(status=Failed, attempts=1)"
        notifDeliveryConsumer      -> notifRepository     "GetByIdAsync(notificationId)"
        notifDeliveryConsumer      -> notifSender         "SendAsync(message) — reintento"
        notifDeliveryConsumer      -> notifRepository     "UpdateAsync(status=Sent)"
        notifDeliveryConsumer      -> compensationPublisher "Publish NotificationDeliveryFailedEvent{attempt+1}"
        notifDeliveryConsumer      -> compensationPublisher "Publish NotificationPermanentlyFailedEvent"
        notifDeliveryConsumer      -> notifRepository     "UpdateAsync(status=PermanentlyFailed, attempts=5)"
        compensationPublisher      -> rabbitmq            "NotificationDeliveryFailedEvent / NotificationPermanentlyFailedEvent" "MassTransit AMQP"
        notifIdempotency           -> notifDbCtx          "SELECT 1 WHERE eventId = ?"
        notifRepository            -> notifDbCtx          "Usa NotificationsDbContext"
        notifDbCtx                 -> notificationsDb     "Lee y escribe"                                   "TCP :5432"
        riskAssessedNotifConsumer  -> rabbitmq            "Escucha RiskAssessedIntegrationEvent"             "MassTransit AMQP"
        notifDeliveryConsumer      -> rabbitmq            "Escucha NotificationDeliveryFailedEvent"           "MassTransit AMQP"
        notifTelemetry             -> tracing             "Envía trazas"                                     "gRPC :4317"
        notifTelemetry             -> monitoring          "Expone /metrics"                                  "HTTP"

        batchVaultLoader    -> vault               "Carga credenciales PostgreSQL, RabbitMQ, URL Credits API" "HTTP :8200"
        batchController     -> recalcJob           "TriggerAsync() — disparo manual vía REST"
        hangfireScheduler   -> recalcJob           "Ejecuta cada 6 h (cron: 0 */6 * * *)"
        hangfireScheduler   -> cleanOutboxJob      "Ejecuta diariamente a 03:00 UTC (cron: 0 3 * * *)"
        hangfireDashboard   -> batchDbCtx          "Lee historial de jobs desde Hangfire tables"
        recalcJob           -> batchCreditsClient  "GET /api/v1/credits?status=Pending,UnderReview"
        batchCreditsClient  -> creditsApi          "HTTP con circuit breaker"                                "HTTP :5001"
        recalcJob           -> batchCreditsClient  "PUT /api/v1/credits/{id}/status (×N)"
        recalcJob           -> recalcPublisher     "Publish RecalculateCreditStatusesRequestedEvent"
        recalcPublisher     -> rabbitmq            "RecalculateCreditStatusesRequestedEvent"                 "MassTransit AMQP"
        recalcJob           -> batchJobRepository  "InsertAsync(JobExecution)"
        cleanOutboxJob      -> batchCreditsClient  "DELETE OutboxMessages WHERE published=true AND publishedAt < NOW()-7d"
        cleanOutboxJob      -> batchJobRepository  "InsertAsync(JobExecution{tipo=CleanOutbox})"
        batchJobRepository  -> batchDbCtx          "Usa BatchDbContext"
        batchDbCtx          -> batchDb             "Lee y escribe"                                           "TCP :5432 / Hangfire"
        batchTelemetry      -> tracing             "Envía trazas"                                            "gRPC :4317"
        batchTelemetry      -> monitoring          "Expone /metrics"                                         "HTTP"
    }

    views {

        systemContext kriteriom "SystemContext" "C4 Nivel 1 — Actores, sistema y dependencias externas." {
            include *
            autoLayout lr
        }

        container kriteriom "Containers" "C4 Nivel 2 — Microservicios, bases de datos, broker y cache." {
            include *
            autoLayout lr
        }

        component gateway "Gateway_Components" "C4 Nivel 3 — Middlewares y proxy del API Gateway." {
            include *
            autoLayout lr
        }

        component creditsApi "CreditsAPI_Components" "C4 Nivel 3 — CQRS, Outbox Pattern y repositorios del Credits API." {
            include *
            autoLayout lr
        }

        component riskApi "RiskAPI_Components" "C4 Nivel 3 — Evaluación de riesgo, idempotencia y publicación de eventos." {
            include *
            autoLayout lr
        }

        component auditApi "AuditAPI_Components" "C4 Nivel 3 — Consumers de auditoría, deduplicación y repositorio inmutable." {
            include *
            autoLayout lr
        }

        component notificationsApi "NotificationsAPI_Components" "C4 Nivel 3 — Saga de compensación y envío de notificaciones." {
            include *
            autoLayout lr
        }

        component batchProcessor "BatchProcessor_Components" "C4 Nivel 3 — Jobs Hangfire, cliente HTTP y publicación de eventos de recálculo." {
            include *
            autoLayout lr
        }

        styles {
            element "Person" {
                shape Person
                background #1168bd
                color #ffffff
            }
            element "Software System" {
                background #1168bd
                color #ffffff
            }
            element "External" {
                background #6b7280
                color #ffffff
            }
            element "Container" {
                background #438dd5
                color #ffffff
            }
            element "Component" {
                background #85bbf0
                color #000000
            }
            element "Database" {
                shape Cylinder
                background #1a6aa8
                color #ffffff
            }
            element "Message Broker" {
                shape Pipe
                background #d97706
                color #ffffff
            }
            element "Cache" {
                shape Cylinder
                background #dc2626
                color #ffffff
            }
            element "Web Browser" {
                shape WebBrowser
                background #438dd5
                color #ffffff
            }
            element "API" {
                shape RoundedBox
                background #438dd5
                color #ffffff
            }
            element "Service" {
                shape Hexagon
                background #438dd5
                color #ffffff
            }
        }

        theme default
    }
}
