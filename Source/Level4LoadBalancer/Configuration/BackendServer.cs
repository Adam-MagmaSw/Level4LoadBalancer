namespace Level4LoadBalancer.Configuration;

public struct BackendServer
{
    public string Host { get; init; }

    public int Port { get; init; }
}