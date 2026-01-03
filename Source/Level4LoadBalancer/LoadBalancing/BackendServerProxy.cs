using Level4LoadBalancer.TcpAbstractions;
using Microsoft.Extensions.Logging;

namespace Level4LoadBalancer.LoadBalancing;

public class BackendServerProxy : IBackendServerProxy
{
    private readonly ILogger<BackendServerProxy> logger;
    private readonly ILoadBalancingStrategy loadBalancingStrategy;
    private readonly ITcpClientFactory tcpClientFactory;
    private readonly IStreamCopier streamCopier;

    public BackendServerProxy(
        ILogger<BackendServerProxy> logger,
        ILoadBalancingStrategy loadBalancingStrategy,
        ITcpClientFactory tcpClientFactory,
        IStreamCopier streamCopier)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.loadBalancingStrategy = loadBalancingStrategy ?? throw new ArgumentNullException(nameof(loadBalancingStrategy));
        this.tcpClientFactory = tcpClientFactory ?? throw new ArgumentNullException(nameof(tcpClientFactory));
        this.streamCopier = streamCopier ?? throw new ArgumentNullException(nameof(streamCopier));
    }

    public async Task ProxyIncomingConnectionToBackendServer(ITcpClientFacade client, CancellationToken cancellationToken)
    {
        try
        {
            var nextBackend = this.loadBalancingStrategy.GetNextBackendServer();

            if (nextBackend is null)
            {
                this.logger.LogError("No backend servers are available to handle the request.");
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
            this.logger.LogError(ex, "An error occurred in LoadBalancerService while processing incoming request.");
        }
        finally
        {
            client.Dispose();
        }
    }
}
