using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace Kriteriom.SharedKernel.Vault;

public static class VaultConfigurationExtensions
{
    /// <summary>
    /// Loads secrets from HashiCorp Vault and merges them into IConfiguration.
    /// Fails gracefully — if Vault is unreachable, logs a warning and continues
    /// with appsettings values (dev mode fallback).
    /// </summary>
    /// <param name="builder">WebApplicationBuilder</param>
    /// <param name="secretPaths">Vault KV v2 paths to load, e.g. "secret/credits"</param>
    public static WebApplicationBuilder AddVaultSecrets(
        this WebApplicationBuilder builder,
        params string[] secretPaths)
    {
        var vaultAddress = builder.Configuration["Vault:Address"] ?? "http://vault:8200";
        var vaultToken   = builder.Configuration["Vault:Token"]   ?? "root-token";

        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var logger = loggerFactory.CreateLogger("VaultConfig");

        try
        {
            var vaultClient = new VaultClient(new VaultClientSettings(
                vaultAddress,
                new TokenAuthMethodInfo(vaultToken)));

            var secrets = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in secretPaths)
            {
                // Parse "secret/credits" → mount="secret", subPath="credits"
                var parts = path.Split('/', 2);
                if (parts.Length != 2) continue;

                var (mount, subPath) = (parts[0], parts[1]);

                var task = vaultClient.V1.Secrets.KeyValue.V2
                    .ReadSecretAsync(subPath, mountPoint: mount);
                if (!task.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException($"Vault read timed out for path '{path}'");
                var kv = task.GetAwaiter().GetResult();

                foreach (var kv2 in kv.Data.Data)
                {
                    // Map vault key "db-connection" → config key "VaultSecrets:db-connection"
                    secrets[$"VaultSecrets:{kv2.Key}"] = kv2.Value?.ToString();
                }

                logger.LogInformation("[Vault] Loaded {Count} secrets from {Path}",
                    kv.Data.Data.Count, path);
            }

            builder.Configuration.AddInMemoryCollection(secrets);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[Vault] Could not connect to Vault at {Address}. " +
                "Falling back to appsettings configuration. " +
                "This is expected during local development without Vault running.",
                vaultAddress);
        }

        return builder;
    }
}
