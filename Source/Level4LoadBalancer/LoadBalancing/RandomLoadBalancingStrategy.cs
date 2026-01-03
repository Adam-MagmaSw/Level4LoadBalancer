using Level4LoadBalancer.Configuration;

namespace Level4LoadBalancer.LoadBalancing;

public class RandomLoadBalancingStrategy : ILoadBalancingStrategy
{
    private readonly IHealthyBackendServerRegister healthyBackendServerRegister;

    public RandomLoadBalancingStrategy(IHealthyBackendServerRegister healthyBackendServerRegister)
    {
        this.healthyBackendServerRegister = healthyBackendServerRegister ?? throw new ArgumentNullException(nameof(healthyBackendServerRegister));
    }

    public BackendServer? GetNextBackendServer()
    {
        var healthyBackendServers = this.healthyBackendServerRegister.GetAllHealthyBackendServers();

        if (healthyBackendServers.Count == 0)
        {
            return null;
        }

        var randomIndex = Random.Shared.Next(healthyBackendServers.Count);
        return healthyBackendServers[randomIndex];
    }
}
