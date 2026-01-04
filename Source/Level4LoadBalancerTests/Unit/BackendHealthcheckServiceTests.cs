using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.Services;
using Level4LoadBalancer.Healthchecking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReceivedExtensions;

namespace Level4LoadBalancerTests.Unit;

[TestFixture]
public class BackendHealthcheckServiceTests
{
    private ILogger<BackendHealthcheckService> logger;
    private IOptions<HealthcheckSettings> healthcheckSettings;
    private IBackendServersHealthChecker backendServersHealthChecker;

    [SetUp]
    public void SetUp()
    {
        logger = Substitute.For<ILogger<BackendHealthcheckService>>();
        healthcheckSettings = Options.Create(new HealthcheckSettings
        {
            IntervalSeconds = 1,
            TimeoutMilliseconds = 1000
        });
        backendServersHealthChecker = Substitute.For<IBackendServersHealthChecker>();
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => 
            new BackendHealthcheckService(null!, healthcheckSettings, backendServersHealthChecker));
    }

    [Test]
    public void Constructor_WithNullHealthcheckSettings_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => 
            new BackendHealthcheckService(logger, null!, backendServersHealthChecker));
    }

    [Test]
    public void Constructor_WithNullBackendServersHealthChecker_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => 
            new BackendHealthcheckService(logger, healthcheckSettings, null!));
    }

    [Test]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var service = new BackendHealthcheckService(logger, healthcheckSettings, backendServersHealthChecker);

        Assert.That(service, Is.Not.Null);
    }

    [Test]
    public async Task ExecuteAsync_CallsHealthcheckAllBackendServersAtLeastOnce()
    {
        var service = new BackendHealthcheckService(logger, healthcheckSettings, backendServersHealthChecker);
        var cts = new CancellationTokenSource();

        backendServersHealthChecker.HealthcheckAllBackendServers(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => cts.Cancel());

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        await backendServersHealthChecker.Received(1).HealthcheckAllBackendServers(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_RespectsIntervalSeconds_BetweenHealthchecks()
    {
        var intervalSettings = Options.Create(new HealthcheckSettings
        {
            IntervalSeconds = 1,
            TimeoutMilliseconds = 1000
        });
        var service = new BackendHealthcheckService(logger, intervalSettings, backendServersHealthChecker);
        var cts = new CancellationTokenSource();

        var callTimes = new List<DateTime>();
        backendServersHealthChecker.HealthcheckAllBackendServers(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ =>
            {
                callTimes.Add(DateTime.UtcNow);
                if (callTimes.Count >= 2)
                {
                    cts.Cancel();
                }
            });

        await service.StartAsync(cts.Token);
        await Task.Delay(2500);
        await service.StopAsync(CancellationToken.None);

        Assert.That(callTimes, Has.Count.GreaterThanOrEqualTo(2));
        var timeDifference = (callTimes[1] - callTimes[0]).TotalSeconds;
        Assert.That(timeDifference, Is.GreaterThanOrEqualTo(0.9).And.LessThan(1.5));
    }

    [Test]
    public async Task ExecuteAsync_StopsGracefully_WhenCancellationRequested()
    {
        var service = new BackendHealthcheckService(logger, healthcheckSettings, backendServersHealthChecker);
        var cts = new CancellationTokenSource();

        backendServersHealthChecker.HealthcheckAllBackendServers(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await service.StartAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await Task.Delay(50);
        await service.StopAsync(CancellationToken.None);

        logger.Received().LogInformation("BackendHealthcheckService is stopping.");
    }
}
