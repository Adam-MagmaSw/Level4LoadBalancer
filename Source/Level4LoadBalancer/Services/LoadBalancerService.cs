using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.LoadBalancingStrategy;
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
    private readonly ILoadBalancingStrategy loadBalancingStrategy;
    private readonly ITcpClientFactory tcpClientFactory;
    private readonly IStreamCopier streamCopier;

    public LoadBalancerService(
        ILogger<LoadBalancerService> logger,
        IOptions<LoadBalancerSettings> loadBalancerSettings,
        ITcpListenerFactory tcpListenerFactory,
        ILoadBalancingStrategy loadBalancingStrategy,
        ITcpClientFactory tcpClientFactory,
        IStreamCopier streamCopier)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.loadBalancerSettings = loadBalancerSettings ?? throw new ArgumentNullException(nameof(loadBalancerSettings));
        this.tcpListenerFactory = tcpListenerFactory ?? throw new ArgumentNullException(nameof(tcpListenerFactory));
        this.loadBalancingStrategy = loadBalancingStrategy ?? throw new ArgumentNullException(nameof(loadBalancingStrategy));
        this.tcpClientFactory = tcpClientFactory ?? throw new ArgumentNullException(nameof(tcpClientFactory));
        this.streamCopier = streamCopier ?? throw new ArgumentNullException(nameof(streamCopier));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("LoadBalancerService is starting.");

        try
        {
            using var tcpListener = this.tcpListenerFactory.CreateAndStart(IPAddress.Any, loadBalancerSettings.Value.Port);

            while (!cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("LoadBalancerService is running at: {time}", DateTimeOffset.Now);

                var client = await tcpListener.AcceptTcpClientAsync(cancellationToken);
                var _ = ProcessIncomingConnection(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopping.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in LoadBalancerService.");
        }
        finally
        {
            logger.LogInformation("LoadBalancerService is stopping.");
        }
    }

    private async Task ProcessIncomingConnection(ITcpClientFacade client, CancellationToken cancellationToken)
    {
        try
        {
            var nextBackend = this.loadBalancingStrategy.GetNextBackendServer();

            if (nextBackend is null)
            {
                logger.LogError("No backend servers are available to handle the request.");
                return;
            }

            using var backEndClient = await this.tcpClientFactory.CreateAndConnect(nextBackend.Value.Host, nextBackend.Value.Port, cancellationToken);

            var clientStream = client.GetStream();
            var backendStream = backEndClient.GetStream();

            await this.streamCopier.CopyAsync(clientStream, backendStream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopping.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in LoadBalancerService while processing incoming request.");
        }
        finally
        {
            client.Dispose();
        }
    }
}
