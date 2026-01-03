
using Level4LoadBalancer.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Level4LoadBalancer;

public class BackendServerRegister : IBackendServerRegister, IHealthyBackendServerRegister
{
    private readonly ConcurrentDictionary<BackendServer, bool> backendServerHealth;

    public BackendServerRegister(IOptions<List<BackendServer>> backendServersOptions)
    {
        var backendServers = backendServersOptions?.Value ?? throw new ArgumentNullException(nameof(backendServersOptions));
        this.backendServerHealth = new ConcurrentDictionary<BackendServer, bool>(
            backendServers.Select(s => new KeyValuePair<BackendServer, bool>(s, true)));

    }

    public IList<BackendServer> GetAllHealthyBackendServers()
    {
        return backendServerHealth
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();
    }

    public IEnumerable<BackendServer> GetAllBackendServers()
    {
        return backendServerHealth.Keys;
    }

    public void RecordBackendServerHealth(BackendServer backend, bool isHealthy)
    {
        backendServerHealth[backend] = isHealthy;
    }
}
