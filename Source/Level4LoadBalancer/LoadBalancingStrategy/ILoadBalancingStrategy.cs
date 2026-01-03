using Level4LoadBalancer.Configuration;

namespace Level4LoadBalancer.LoadBalancingStrategy;

public interface ILoadBalancingStrategy
{
    BackendServer? GetNextBackendServer();
}
