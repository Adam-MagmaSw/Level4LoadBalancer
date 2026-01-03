using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.TcpAbstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Level4LoadBalancer.Healthchecking;

public class BackendServersHealthChecker : IBackendServersHealthChecker
{
    private readonly ILogger<BackendServersHealthChecker> logger;
    private readonly IOptions<HealthcheckSettings> healthcheckSettings;
    private readonly IBackendServerRegister backendServerRegister;
    private readonly ITcpClientFactory tcpClientFactory;

    public BackendServersHealthChecker(
        ILogger<BackendServersHealthChecker> logger,
        IOptions<HealthcheckSettings> healthcheckSettings,
        IBackendServerRegister backendServerRegister,
        ITcpClientFactory tcpClientFactory)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.healthcheckSettings = healthcheckSettings ?? throw new ArgumentNullException(nameof(healthcheckSettings));
        this.backendServerRegister = backendServerRegister ?? throw new ArgumentNullException(nameof(backendServerRegister));
        this.tcpClientFactory = tcpClientFactory ?? throw new ArgumentNullException(nameof(tcpClientFactory));
    }

    public async Task HealthcheckAllBackendServers(CancellationToken cancellationToken)
    {
        var backendServers = this.backendServerRegister.GetAllBackendServers();
        var healthcheckTasks = backendServers.Select(backendServer => CheckBackendServerHealthAsync(backendServer, cancellationToken));
        await Task.WhenAll(healthcheckTasks);
    }

    private async Task CheckBackendServerHealthAsync(BackendServer backendServer, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMilliseconds(this.healthcheckSettings.Value.TimeoutMilliseconds);

        try
        {
            using var client = await this.tcpClientFactory.CreateAndConnect(backendServer.Host, backendServer.Port, cancellationToken)
                .WaitAsync(timeout, cancellationToken);

            this.backendServerRegister.RecordBackendServerHealth(backendServer, true);
            this.logger.LogInformation("{Host}:{Port} is healthy", backendServer.Host, backendServer.Port);
        }
        catch (TimeoutException ex)
        {
            this.backendServerRegister.RecordBackendServerHealth(backendServer, false);
            this.logger.LogWarning(ex, "{Host}:{Port} timed out", backendServer.Host, backendServer.Port);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            this.backendServerRegister.RecordBackendServerHealth(backendServer, false);
            this.logger.LogWarning("{Host}:{Port} is unhealthy.", backendServer.Host, backendServer.Port);
        }
    }
}
