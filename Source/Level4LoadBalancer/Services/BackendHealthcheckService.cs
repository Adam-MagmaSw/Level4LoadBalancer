using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.Healthchecking;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Level4LoadBalancer.Services;

public class BackendHealthcheckService : BackgroundService
{
    private readonly ILogger<BackendHealthcheckService> logger;
    private readonly IOptions<HealthcheckSettings> healthcheckSettings;
    private readonly IBackendServersHealthChecker backendServersHealthChecker;

    public BackendHealthcheckService(
        ILogger<BackendHealthcheckService> logger,
        IOptions<HealthcheckSettings> healthcheckSettings,
        IBackendServersHealthChecker backendServersHealthChecker)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.healthcheckSettings = healthcheckSettings ?? throw new ArgumentNullException(nameof(healthcheckSettings));
        this.backendServersHealthChecker = backendServersHealthChecker ?? throw new ArgumentNullException(nameof(backendServersHealthChecker));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("BackendHealthcheckService is starting.");

        while (!cancellationToken.IsCancellationRequested)
        {
            this.logger.LogInformation("BackendHealthcheckService is running at: {time}", DateTimeOffset.Now);

            await this.backendServersHealthChecker.HealthcheckAllBackendServers(cancellationToken);

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(this.healthcheckSettings.Value.IntervalSeconds),
                    cancellationToken);
            }
            catch (TaskCanceledException)
            {
            }
        }

        this.logger.LogInformation("BackendHealthcheckService is stopping.");
    }
}
