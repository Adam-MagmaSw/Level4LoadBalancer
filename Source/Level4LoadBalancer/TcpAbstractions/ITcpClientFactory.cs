
namespace Level4LoadBalancer.TcpAbstractions;

public interface ITcpClientFactory
{
    Task<ITcpClientFacade> CreateAndConnect(string host, int port, CancellationToken cancellationToken);
}