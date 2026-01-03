
using Level4LoadBalancer.Configuration;

namespace Level4LoadBalancer;

public interface IBackendServerRegister
{
    IEnumerable<BackendServer> GetAllBackendServers();
    void RecordBackendServerHealth(BackendServer backend, bool isHealthy);
}