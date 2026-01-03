using Level4LoadBalancer.TcpAbstractions;

namespace Level4LoadBalancer.LoadBalancing
{
    public interface IBackendServerProxy
    {
        Task ProxyIncomingConnectionToBackendServer(ITcpClientFacade client, CancellationToken cancellationToken);
    }
}