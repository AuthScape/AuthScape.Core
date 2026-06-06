using AuthScape.Saml2.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Context;

namespace AuthScape.Saml2;

/// <summary>
/// Periodically refreshes IdP metadata for each enabled SamlConfiguration that has an IdpMetadataUrl.
/// Implements the protections described in security gotcha #1 of the plan:
///   - Persistent metadata cache: writes to SamlConfiguration.IdpMetadataXml (read by SamlService at validation time)
///   - Fail-soft: a fetch failure leaves the existing cached XML in place
///   - Operational signals: emits warnings/errors via ILogger that bubble to LogService
///
/// Cadence: every config's MetadataRefreshIntervalHours (default 6h), with a small jitter to avoid
/// stampeding when many tenants are configured. The service tick runs every 30 minutes; per-config
/// scheduling is decided each tick based on MetadataLastRefreshedAt.
/// </summary>
public class SamlMetadataRefreshService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(15);
    private const int FailureThresholdForError = 3;

    private readonly IServiceScopeFactory scopeFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<SamlMetadataRefreshService> logger;
    private readonly Random jitter = new();

    public SamlMetadataRefreshService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<SamlMetadataRefreshService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial random delay so multiple instances don't all hit IdPs simultaneously after a deploy.
        try { await Task.Delay(TimeSpan.FromSeconds(jitter.Next(5, 60)), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in SAML metadata refresh tick");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunTickAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var configs = await db.Set<SamlConfiguration>()
            .Where(c => c.IsEnabled && c.IdpMetadataUrl != null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var config in configs)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var due = config.MetadataLastRefreshedAt == null
                || (now - config.MetadataLastRefreshedAt.Value)
                    >= TimeSpan.FromHours(Math.Max(config.MetadataRefreshIntervalHours, 1));
            if (!due) continue;

            await RefreshOneAsync(db, config, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RefreshOneAsync(DatabaseContext db, SamlConfiguration config, CancellationToken cancellationToken)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = FetchTimeout;

            var xml = await http.GetStringAsync(config.IdpMetadataUrl!, cancellationToken);

            // Minimal validation: parse to ensure it's well-formed XML.
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(xml);

            config.IdpMetadataXml = xml;
            config.MetadataLastRefreshedAt = DateTime.UtcNow;
            config.MetadataConsecutiveFailures = 0;

            logger.LogInformation("Refreshed SAML metadata for config {ConfigId} ({Name})", config.Id, config.Name);
        }
        catch (Exception ex)
        {
            config.MetadataConsecutiveFailures++;
            var level = config.MetadataConsecutiveFailures >= FailureThresholdForError
                ? LogLevel.Error
                : LogLevel.Warning;
            logger.Log(level, ex,
                "SAML metadata refresh failed for config {ConfigId} ({Name}); consecutive failures: {Failures}. Cached XML preserved.",
                config.Id, config.Name, config.MetadataConsecutiveFailures);
        }
    }
}
