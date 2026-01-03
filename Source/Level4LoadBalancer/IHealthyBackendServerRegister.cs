
using Level4LoadBalancer.Configuration;

namespace Level4LoadBalancer;

public interface IHealthyBackendServerRegister
{
    IList<BackendServer> GetAllHealthyBackendServers();
}