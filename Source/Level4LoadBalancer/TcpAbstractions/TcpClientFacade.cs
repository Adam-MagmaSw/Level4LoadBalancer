using System.Net;
using System.Net.Sockets;

namespace Level4LoadBalancer.TcpAbstractions;

public class TcpClientFacade : ITcpClientFacade
{
    private readonly TcpClient client;

    public TcpClientFacade(TcpClient client)
    {
        this.client = client;
    }

    public Stream GetStream() => client.GetStream();

    public Task ConnectAsync(string ip, int port)
    {
        return client.ConnectAsync(IPAddress.Parse(ip), port);
    }

    public void Dispose()
    {
        client.Dispose();
    }
}
