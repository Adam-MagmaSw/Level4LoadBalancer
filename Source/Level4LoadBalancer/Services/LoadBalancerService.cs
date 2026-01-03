using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.LoadBalancing;
using Level4LoadBalancer.TcpAbstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace Level4LoadBalancer.Services;

public class LoadBalancerService : BackgroundService
{
    private readonly ILogger<LoadBalancerService> logger;
    private readonly IOptions<LoadBalancerSettings> loadBalancerSettings;
    private readonly ITcpListenerFactory tcpListenerFactory;
    private readonly IBackendServerProxy backendServerProxy;

    public LoadBalancerService(
        ILogger<LoadBalancerService> logger,
        IOptions<LoadBalancerSettings> loadBalancerSettings,
        ITcpListenerFactory tcpListenerFactory,
        IBackendServerProxy backendServerProxy)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.loadBalancerSettings = loadBalancerSettings ?? throw new ArgumentNullException(nameof(loadBalancerSettings));
        this.tcpListenerFactory = tcpListenerFactory ?? throw new ArgumentNullException(nameof(tcpListenerFactory));
        this.backendServerProxy = backendServerProxy ?? throw new ArgumentNullException(nameof(backendServerProxy));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("LoadBalancerService is starting.");

        try
        {
            using var tcpListener = this.tcpListenerFactory.CreateAndStart(IPAddress.Any, loadBalancerSettings.Value.Port);

            while (!cancellationToken.IsCancellationRequested)
            {
                this.logger.LogInformation("LoadBalancerService is running at: {time}", DateTimeOffset.Now);

                var client = await tcpListener.AcceptTcpClientAsync(cancellationToken);
                var _ = backendServerProxy.ProxyIncomingConnectionToBackendServer(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopping.
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "An error occurred in LoadBalancerService.");
        }
        finally
        {
            this.logger.LogInformation("LoadBalancerService is stopping.");
        }
    }
}
