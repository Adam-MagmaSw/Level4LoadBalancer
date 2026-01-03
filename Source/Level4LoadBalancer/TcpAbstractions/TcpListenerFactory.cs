using System.Net;

namespace Level4LoadBalancer.TcpAbstractions;

public class TcpListenerFactory : ITcpListenerFactory
{
    public ITcpListenerFacade CreateAndStart(IPAddress iPAddress, int port)
    {
        var tcpListenerFacade = new TcpListenerFacade(iPAddress, port);
        tcpListenerFacade.Start();
        return tcpListenerFacade;
    }
}
