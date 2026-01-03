namespace Level4LoadBalancer.Configuration;

public class HealthcheckSettings
{
    public int IntervalSeconds { get; init; }

    public int TimeoutMilliseconds { get; init; }
}
