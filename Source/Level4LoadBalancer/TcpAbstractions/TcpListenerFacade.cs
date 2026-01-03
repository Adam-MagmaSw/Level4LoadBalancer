using System.Net;
using System.Net.Sockets;

namespace Level4LoadBalancer.TcpAbstractions;

public class TcpListenerFacade : ITcpListenerFacade
{
    private readonly TcpListener tcpListener;

    public TcpListenerFacade(IPAddress localaddr, int port)
    {
        tcpListener = new TcpListener(localaddr, port);
    }

    public void Start()
    {
        tcpListener.Start();
    }

    public void Stop()
    {
        tcpListener.Stop();
    }

    public async Task<ITcpClientFacade> AcceptTcpClientAsync(CancellationToken cancellationToken)
    {
        return new TcpClientFacade(await tcpListener.AcceptTcpClientAsync(cancellationToken));
    }

    public void Dispose()
    {
        tcpListener.Dispose();
    }
}
