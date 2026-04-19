using System.Text;
using FluentValidation;
using Kriteriom.Credits.API.Middleware;
using Kriteriom.Credits.Application.Commands.CreateCredit;
using Kriteriom.Credits.Infrastructure;
using Kriteriom.Credits.Infrastructure.Persistence;
using Kriteriom.SharedKernel.CQRS;
using Kriteriom.SharedKernel.Extensions;
using Kriteriom.SharedKernel.Middleware;
using Kriteriom.SharedKernel.Vault;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

SerilogExtensions.ConfigureBootstrapLogger();

try
{
    Log.Information("Starting Credits API");

    var builder = WebApplication.CreateBuilder(args);
    builder.AddVaultSecrets("secret/credits", "secret/auth");

    builder.Host.AddServiceSerilog(
        enrichWithMachineInfo: true,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");

    var jwtSecret   = builder.Configuration["Jwt:Secret"]   ?? "K3r1t3r10m-Sup3rS3cr3t-JWT-K3y-2026-ForDev";
    var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "kriteriom-api-gateway";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "kriteriom-services";

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer           = true,
                ValidIssuer              = jwtIssuer,
                ValidateAudience         = true,
                ValidAudience            = jwtAudience,
                ClockSkew                = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(CreateCreditCommand).Assembly);
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
    });

    builder.Services.AddValidatorsFromAssemblyContaining<CreateCreditCommandValidator>();

    builder.Services.AddCreditsInfrastructure(builder.Configuration);

    builder.Services.AddServiceTelemetry(builder.Configuration, "credits-api", includeEfCore: true);

    builder.Services.AddApiVersioning(opts =>
    {
        opts.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        opts.AssumeDefaultVersionWhenUnspecified = true;
        opts.ReportApiVersions = true;
        opts.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
            new Asp.Versioning.UrlSegmentApiVersionReader(),
            new Asp.Versioning.HeaderApiVersionReader("api-version"),
            new Asp.Versioning.QueryStringApiVersionReader("api-version"));
    }).AddApiExplorer(opts =>
    {
        opts.GroupNameFormat = "'v'VVV";
        opts.SubstituteApiVersionInUrl = true;
    });

    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
            opts.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter()));

    builder.Services.AddServiceSwagger(
        "Kriteriom Credits API",
        "Credit management service with CQRS, Outbox Pattern, and Idempotency",
        c =>
        {
            c.AddSecurityDefinition("IdempotencyKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "Idempotency-Key",
                Description = "Unique key to ensure idempotent processing of POST requests"
            });

            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                c.IncludeXmlComments(xmlPath);
        });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<CreditsDbContext>("database")
        .AddRedis(
            redisConnectionString: builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379",
            name: "redis");

    var app = builder.Build();

    await app.RunMigrationsAsync<CreditsDbContext>();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlerMiddleware>();

    app.UseSerilogRequestLogging(opts =>
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms");

    app.UseServiceSwagger("Credits API");
    app.UseServiceMetrics("credits-api");

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.Run();
}
catch (Exception ex) when (ex is not OperationCanceledException && ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "Credits API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
