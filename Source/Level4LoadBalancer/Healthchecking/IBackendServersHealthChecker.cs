
namespace Level4LoadBalancer.Healthchecking
{
    public interface IBackendServersHealthChecker
    {
        Task HealthcheckAllBackendServers(CancellationToken cancellationToken);
    }
}