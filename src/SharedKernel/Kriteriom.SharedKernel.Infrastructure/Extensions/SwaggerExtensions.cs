using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kriteriom.SharedKernel.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddServiceSwagger(
        this IServiceCollection services,
        string title,
        string description = "",
        Action<SwaggerGenOptions>? configure = null)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = title, Version = "v1", Description = description });
            configure?.Invoke(c);
        });

        return services;
    }

    public static IApplicationBuilder UseServiceSwagger(this IApplicationBuilder app, string title)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{title} v1");
            c.RoutePrefix = "swagger";
        });

        return app;
    }
}
