using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Infrastructure.Seeding;

public class CreditsDataSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<CreditsDataSeeder> logger) : IHostedService
{
    private static readonly (string FullName, string Email, string Doc, decimal Income, EmploymentStatus Status)[] ClientData =
    [
        ("Carlos Méndez Ruiz",      "carlos.mendez@email.com",    "CC-12345001", 5_500_000m,  EmploymentStatus.Employed),
        ("Ana García Torres",       "ana.garcia@email.com",       "CC-12345002", 3_200_000m,  EmploymentStatus.Employed),
        ("Luis Torres Vargas",      "luis.torres@email.com",      "CC-12345003", 8_000_000m,  EmploymentStatus.SelfEmployed),
        ("María López Soto",        "maria.lopez@email.com",      "CC-12345004", 2_800_000m,  EmploymentStatus.Employed),
        ("Jorge Ramírez Cruz",      "jorge.ramirez@email.com",    "CC-12345005", 12_000_000m, EmploymentStatus.SelfEmployed),
        ("Sandra Flores Peña",      "sandra.flores@email.com",    "CC-12345006", 4_100_000m,  EmploymentStatus.Employed),
        ("Pablo Herrera Díaz",      "pablo.herrera@email.com",    "CC-12345007", 1_200_000m,  EmploymentStatus.Employed),
        ("Laura Castillo Vega",     "laura.castillo@email.com",   "CC-12345008", 6_500_000m,  EmploymentStatus.Employed),
        ("Andrés Moreno Jiménez",   "andres.moreno@email.com",    "CC-12345009", 3_700_000m,  EmploymentStatus.Employed),
        ("Camila Vega Rojas",       "camila.vega@email.com",      "CC-12345010", 9_000_000m,  EmploymentStatus.SelfEmployed),
        ("Ricardo Peña Silva",      "ricardo.pena@email.com",     "CC-12345011", 2_200_000m,  EmploymentStatus.Unemployed),
        ("Diana Cruz Reyes",        "diana.cruz@email.com",       "CC-12345012", 7_300_000m,  EmploymentStatus.Employed),
        ("Fernando Ruiz Morales",   "fernando.ruiz@email.com",    "CC-12345013", 4_800_000m,  EmploymentStatus.SelfEmployed),
        ("Valentina Soto Herrera",  "valentina.soto@email.com",   "CC-12345014", 5_000_000m,  EmploymentStatus.Employed),
        ("Sebastián Vargas López",  "sebastian.vargas@email.com", "CC-12345015", 1_500_000m,  EmploymentStatus.Employed),
        ("Isabella Jiménez García", "isabella.jimenez@email.com", "CC-12345016", 11_000_000m, EmploymentStatus.Employed),
        ("Tomás Rojas Castillo",    "tomas.rojas@email.com",      "CC-12345017", 3_300_000m,  EmploymentStatus.SelfEmployed),
        ("Natalia Díaz Vargas",     "natalia.diaz@email.com",     "CC-12345018", 6_100_000m,  EmploymentStatus.Employed),
        ("Alejandro Reyes Cruz",    "alejandro.reyes@email.com",  "CC-12345019", 2_600_000m,  EmploymentStatus.Retired),
        ("Gabriela Silva Mendoza",  "gabriela.silva@email.com",   "CC-12345020", 4_500_000m,  EmploymentStatus.Employed),
    ];

    private static readonly (int ClientIdx, decimal Amount, decimal Rate, int TermMonths)[] CreditData =
    [
        (0,  8_000_000m,  0.12m, 36), (2,  15_000_000m, 0.10m, 48), (5,  5_000_000m,  0.09m, 24),
        (7,  10_000_000m, 0.11m, 36), (9,  20_000_000m, 0.10m, 60), (11, 8_000_000m,  0.10m, 36),
        (13, 6_000_000m,  0.09m, 24), (15, 12_000_000m, 0.10m, 48), (17, 9_000_000m,  0.10m, 36),
        (1,  5_000_000m,  0.15m, 36), (8,  10_000_000m, 0.14m, 36), (12, 8_000_000m,  0.13m, 36),
        (16, 4_000_000m,  0.12m, 24), (18, 4_000_000m,  0.11m, 36),
        (6,  10_000_000m, 0.18m, 36), (10, 8_000_000m,  0.15m, 36), (14, 12_000_000m, 0.20m, 48),
        (3,  15_000_000m, 0.20m, 60), (4,  1_000_000m,  0.05m, 12), (19, 2_000_000m,  0.08m, 24),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CreditsDbContext>();

        if (await db.Clients.AnyAsync(cancellationToken))
        {
            logger.LogInformation("CreditsDataSeeder: datos existentes, omitiendo seed.");
            return;
        }

        logger.LogInformation("CreditsDataSeeder: iniciando con {C} clientes y {Cr} créditos",
            ClientData.Length, CreditData.Length);

        var clients = ClientData
            .Select(c => Client.Create(c.FullName, c.Email, c.Doc, c.Income, c.Status))
            .ToArray();

        db.Clients.AddRange(clients);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("CreditsDataSeeder: {Count} clientes persistidos", clients.Length);

        int ok = 0, skipped = 0;
        foreach (var (idx, amount, rate, term) in CreditData)
        {
            if (idx >= clients.Length) { skipped++; continue; }

            var credit = Credit.Create(clients[idx].Id, amount, rate, term);
            db.Credits.Add(credit);
            ok++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("CreditsDataSeeder: {Ok} créditos persistidos, {Skipped} omitidos", ok, skipped);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
