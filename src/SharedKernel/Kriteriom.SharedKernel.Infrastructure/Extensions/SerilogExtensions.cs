using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Kriteriom.SharedKernel.Extensions;

public static class SerilogExtensions
{
    public static void ConfigureBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    public static IHostBuilder AddServiceSerilog(
        this IHostBuilder host,
        bool enrichWithMachineInfo = false,
        string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    {
        host.UseSerilog((context, services, configuration) =>
        {
            var cfg = configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: outputTemplate);

            if (enrichWithMachineInfo)
                cfg.Enrich.WithMachineName().Enrich.WithEnvironmentName();
        });

        return host;
    }
}
