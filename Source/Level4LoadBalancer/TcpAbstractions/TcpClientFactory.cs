using System.Net.Sockets;

namespace Level4LoadBalancer.TcpAbstractions;

public class TcpClientFactory : ITcpClientFactory
{
    public async Task<ITcpClientFacade> CreateAndConnect(string host, int port, CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken);
        return new TcpClientFacade(client);
    }
}
