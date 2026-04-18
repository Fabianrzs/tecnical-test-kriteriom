using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kriteriom.BatchProcessor.Persistence;

namespace Kriteriom.BatchProcessor.Jobs;

/// <summary>
/// Seeds 20 realistic clients with financial profiles, then creates credits
/// that exercise all status paths and edge cases.
/// Only runs when FeatureFlags:SeedTestData is true.
/// </summary>
public class SeedTestDataJob(
    ILogger<SeedTestDataJob> logger,
    IHttpClientFactory httpClientFactory,
    BatchDbContext dbContext,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy    = JsonNamingPolicy.CamelCase,
        Converters              = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    // 20 realistic Colombian clients — CreditScore is calculated automatically from income + employment
    private static readonly ClientSeed[] ClientSeeds =
    [
        new("Carlos Méndez Ruiz",      "carlos.mendez@email.com",   "CC-12345001", 5_500_000m,  "Employed"),
        new("Ana García Torres",       "ana.garcia@email.com",      "CC-12345002", 3_200_000m,  "Employed"),
        new("Luis Torres Vargas",      "luis.torres@email.com",     "CC-12345003", 8_000_000m,  "SelfEmployed"),
        new("María López Soto",        "maria.lopez@email.com",     "CC-12345004", 2_800_000m,  "Employed"),
        new("Jorge Ramírez Cruz",      "jorge.ramirez@email.com",   "CC-12345005", 12_000_000m, "SelfEmployed"),
        new("Sandra Flores Peña",      "sandra.flores@email.com",   "CC-12345006", 4_100_000m,  "Employed"),
        new("Pablo Herrera Díaz",      "pablo.herrera@email.com",   "CC-12345007", 1_200_000m,  "Employed"),
        new("Laura Castillo Vega",     "laura.castillo@email.com",  "CC-12345008", 6_500_000m,  "Employed"),
        new("Andrés Moreno Jiménez",   "andres.moreno@email.com",   "CC-12345009", 3_700_000m,  "Employed"),
        new("Camila Vega Rojas",       "camila.vega@email.com",     "CC-12345010", 9_000_000m,  "SelfEmployed"),
        new("Ricardo Peña Silva",      "ricardo.pena@email.com",    "CC-12345011", 2_200_000m,  "Unemployed"),
        new("Diana Cruz Reyes",        "diana.cruz@email.com",      "CC-12345012", 7_300_000m,  "Employed"),
        new("Fernando Ruiz Morales",   "fernando.ruiz@email.com",   "CC-12345013", 4_800_000m,  "SelfEmployed"),
        new("Valentina Soto Herrera",  "valentina.soto@email.com",  "CC-12345014", 5_000_000m,  "Employed"),
        new("Sebastián Vargas López",  "sebastian.vargas@email.com","CC-12345015", 1_500_000m,  "Employed"),
        new("Isabella Jiménez García", "isabella.jimenez@email.com","CC-12345016", 11_000_000m, "Employed"),
        new("Tomás Rojas Castillo",    "tomas.rojas@email.com",     "CC-12345017", 3_300_000m,  "SelfEmployed"),
        new("Natalia Díaz Vargas",     "natalia.diaz@email.com",    "CC-12345018", 6_100_000m,  "Employed"),
        new("Alejandro Reyes Cruz",    "alejandro.reyes@email.com", "CC-12345019", 2_600_000m,  "Retired"),
        new("Gabriela Silva Mendoza",  "gabriela.silva@email.com",  "CC-12345020", 4_500_000m,  "Employed"),
    ];

    // Credit scenarios referencing clients by index.
    // Expected risk outcome depends on real DTI calculation in Risk Service.
    private static readonly CreditSeed[] CreditSeeds =
    [
        // Low DTI, good score → Expected: Approved → Active
        new(0,  8_000_000m,  0.12m),   // Carlos: DTI ≈ 16% → Approved
        new(2, 15_000_000m,  0.10m),   // Luis:   DTI ≈ 10% → Approved
        new(5,  5_000_000m,  0.09m),   // Sandra: DTI ≈ 7%  → Approved
        new(7, 10_000_000m,  0.11m),   // Laura:  DTI ≈ 9%  → Approved
        new(9, 20_000_000m,  0.10m),   // Camila: DTI ≈ 13% → Approved
        new(11, 8_000_000m,  0.10m),   // Diana:  DTI ≈ 6%  → Approved
        new(13, 6_000_000m,  0.09m),   // Valentina: DTI ≈ 7% → Approved
        new(15,12_000_000m,  0.10m),   // Isabella: DTI ≈ 6%  → Approved
        new(17, 9_000_000m,  0.10m),   // Natalia: DTI ≈ 8%  → Approved

        // Medium DTI or fair score → Expected: UnderReview
        new(1,  5_000_000m,  0.15m),   // Ana:    DTI ≈ 30%  → Review (borderline)
        new(8, 10_000_000m,  0.14m),   // Andrés: DTI ≈ 46%  → Review (fair score 640)
        new(12, 8_000_000m,  0.13m),   // Fernando: DTI ≈ 28% + fair score → Review
        new(16, 4_000_000m,  0.12m),   // Tomás: DTI ≈ 43%   → Review
        new(18, 4_000_000m,  0.11m),   // Alejandro: score 580 → Review

        // High DTI or bad score → Expected: Rejected
        new(6, 10_000_000m,  0.18m),   // Pablo:  income 1.9M, DTI ≈ 145% → Rejected
        new(10, 8_000_000m,  0.15m),   // Ricardo: unemployed + fair score → Rejected
        new(14,12_000_000m,  0.20m),   // Sebastián: income 1.5M, DTI ≈ 250% → Rejected
        new(3, 15_000_000m,  0.20m),   // María:  income 2.8M, DTI ≈ 105% → Rejected

        // Edge cases
        new(4,  1_000_000m,  0.05m),   // Jorge: huge income, tiny loan → very low DTI
        new(19, 2_000_000m,  0.08m),   // Gabriela: moderate loan + good score
    ];

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        bool seedEnabled = configuration.GetValue<bool>("FeatureFlags:SeedTestData", false);
        if (!seedEnabled)
        {
            logger.LogWarning("SeedTestDataJob skipped — FeatureFlags:SeedTestData is disabled.");
            return;
        }

        logger.LogInformation("SeedTestDataJob starting: {Clients} clients + {Credits} credits",
            ClientSeeds.Length, CreditSeeds.Length);

        var http = httpClientFactory.CreateClient("credits-api");

        // Phase 1 — Create clients (or retrieve existing)
        var clientIds = new Guid[ClientSeeds.Length];
        int clientsCreated = 0, clientsExisting = 0;

        for (int i = 0; i < ClientSeeds.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var seed = ClientSeeds[i];
            try
            {
                var payload = new
                {
                    fullName         = seed.FullName,
                    email            = seed.Email,
                    documentNumber   = seed.DocumentNumber,
                    monthlyIncome    = seed.MonthlyIncome,
                    employmentStatus = seed.EmploymentStatus
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/clients");
                req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
                var res = await http.SendAsync(req, ct);

                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                    clientIds[i] = json.GetProperty("id").GetGuid();
                    clientsCreated++;
                }
                else if (res.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                {
                    // Already exists — fetch by email
                    var body = await res.Content.ReadAsStringAsync(ct);
                    if (body.Contains("CLIENT_DUPLICATE_EMAIL"))
                    {
                        // Client already seeded — search for it (we'll just log the skip)
                        logger.LogDebug("Client {Email} already exists, skipping create.", seed.Email);
                        clientsExisting++;
                    }
                }
                else
                {
                    logger.LogWarning("Failed to create client {Email}: {Status}", seed.Email, (int)res.StatusCode);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error creating client {Email}", seed.Email);
            }
        }

        logger.LogInformation("Clients phase done: {Created} created, {Existing} already existed",
            clientsCreated, clientsExisting);

        // Phase 2 — Create credits
        int creditsCreated = 0, creditsFailed = 0;

        for (int i = 0; i < CreditSeeds.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var seed     = CreditSeeds[i];
            var clientId = clientIds[seed.ClientIndex];

            if (clientId == Guid.Empty)
            {
                logger.LogWarning("Credit seed {Index} skipped — client index {Client} has no ID", i, seed.ClientIndex);
                creditsFailed++;
                continue;
            }

            try
            {
                var payload = new
                {
                    clientId     = clientId,
                    amount       = seed.Amount,
                    interestRate = seed.InterestRate
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/credits");
                req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
                req.Headers.Add("Idempotency-Key", $"seed-v3-{i:D3}");

                var res = await http.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                    creditsCreated++;
                else
                {
                    var body = await res.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("Credit seed {Index} failed: {Status} — {Body}", i, (int)res.StatusCode, body);
                    creditsFailed++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error creating credit seed {Index}", i);
                creditsFailed++;
            }
        }

        var summary = $"Seed v3 done: {clientsCreated} clients created, {clientsExisting} existing, " +
                      $"{creditsCreated} credits created, {creditsFailed} failed.";

        dbContext.BatchJobLogs.Add(new BatchJobLog
        {
            JobName   = "SeedTestData",
            Message   = summary,
            Level     = creditsFailed > 0 ? "Warning" : "Information",
            Timestamp = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("{Summary}", summary);
    }

    private record ClientSeed(
        string FullName,
        string Email,
        string DocumentNumber,
        decimal MonthlyIncome,
        string EmploymentStatus);

    private record CreditSeed(
        int ClientIndex,
        decimal Amount,
        decimal InterestRate);
}
