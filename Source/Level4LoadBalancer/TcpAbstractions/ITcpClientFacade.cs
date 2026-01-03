using System.Net;

namespace Level4LoadBalancer.TcpAbstractions;

public interface ITcpClientFacade : IDisposable
{
    Task ConnectAsync(string ip, int port);

    Stream GetStream();
}