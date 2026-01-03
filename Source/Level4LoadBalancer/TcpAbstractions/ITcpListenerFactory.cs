using System.Net;

namespace Level4LoadBalancer.TcpAbstractions;

public interface ITcpListenerFactory
{
    ITcpListenerFacade CreateAndStart(IPAddress iPAddress, int port);
}