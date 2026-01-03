namespace Level4LoadBalancer.TcpAbstractions;

public interface ITcpListenerFacade : IDisposable
{
    Task<ITcpClientFacade> AcceptTcpClientAsync(CancellationToken cancellationToken);

    void Start();

    void Stop();
}