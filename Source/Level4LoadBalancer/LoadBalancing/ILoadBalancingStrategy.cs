using Level4LoadBalancer.Configuration;

namespace Level4LoadBalancer.LoadBalancing;

public interface ILoadBalancingStrategy
{
    BackendServer? GetNextBackendServer();
}
