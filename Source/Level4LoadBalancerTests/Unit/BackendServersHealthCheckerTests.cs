using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.Healthchecking;
using Level4LoadBalancer;
using Level4LoadBalancer.TcpAbstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Level4LoadBalancerTests.Unit;

[TestFixture]
public class BackendServersHealthCheckerTests
{
    private ILogger<BackendServersHealthChecker> logger;
    private IOptions<HealthcheckSettings> healthcheckSettings;
    private IBackendServerRegister backendServerRegister;
    private ITcpClientFactory tcpClientFactory;

    [SetUp]
    public void SetUp()
    {
        logger = Substitute.For<ILogger<BackendServersHealthChecker>>();
        healthcheckSettings = Options.Create(new HealthcheckSettings
        {
            IntervalSeconds = 10,
            TimeoutMilliseconds = 1000
        });
        backendServerRegister = Substitute.For<IBackendServerRegister>();
        tcpClientFactory = Substitute.For<ITcpClientFactory>();
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendServersHealthChecker(null!, healthcheckSettings, backendServerRegister, tcpClientFactory));
    }

    [Test]
    public void Constructor_WithNullHealthcheckSettings_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendServersHealthChecker(logger, null!, backendServerRegister, tcpClientFactory));
    }

    [Test]
    public void Constructor_WithNullBackendServerRegister_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendServersHealthChecker(logger, healthcheckSettings, null!, tcpClientFactory));
    }

    [Test]
    public void Constructor_WithNullTcpClientFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, null!));
    }

    [Test]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var healthChecker = new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);

        Assert.That(healthChecker, Is.Not.Null);
    }

    [Test]
    public async Task HealthcheckAllBackendServers_WithNoServers_CompletesSuccessfully()
    {
        backendServerRegister.GetAllBackendServers().Returns(new List<BackendServer>());
        var healthChecker = new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);

        await healthChecker.HealthcheckAllBackendServers(CancellationToken.None);

        backendServerRegister.Received(1).GetAllBackendServers();
        await tcpClientFactory.DidNotReceive().CreateAndConnect(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HealthcheckAllBackendServers_WithHealthyServer_RecordsHealthyStatus()
    {
        var server = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { server });
        var mockClient = Substitute.For<ITcpClientFacade>();
        tcpClientFactory.CreateAndConnect("localhost", 8001, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient));

        var healthChecker = new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);

        await healthChecker.HealthcheckAllBackendServers(CancellationToken.None);

        backendServerRegister.Received(1).RecordBackendServerHealth(server, true);
        mockClient.Received(1).Dispose();
    }

    [Test]
    public async Task HealthcheckAllBackendServers_WithMultipleHealthyServers_RecordsAllAsHealthy()
    {
        var server1 = new BackendServer { Host = "server1", Port = 8001 };
        var server2 = new BackendServer { Host = "server2", Port = 8002 };
        var server3 = new BackendServer { Host = "server3", Port = 8003 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { server1, server2, server3 });

        var mockClient1 = Substitute.For<ITcpClientFacade>();
        var mockClient2 = Substitute.For<ITcpClientFacade>();
        var mockClient3 = Substitute.For<ITcpClientFacade>();

        tcpClientFactory.CreateAndConnect("server1", 8001, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient1));
        tcpClientFactory.CreateAndConnect("server2", 8002, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient2));
        tcpClientFactory.CreateAndConnect("server3", 8003, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient3));

        var healthChecker = new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);

        await healthChecker.HealthcheckAllBackendServers(CancellationToken.None);

        backendServerRegister.Received(1).RecordBackendServerHealth(server1, true);
        backendServerRegister.Received(1).RecordBackendServerHealth(server2, true);
        backendServerRegister.Received(1).RecordBackendServerHealth(server3, true);
    }

    [Test]
    public async Task HealthcheckAllBackendServers_WithTimeout_RecordsUnhealthyStatus()
    {
        var server = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { server });
        tcpClientFactory.CreateAndConnect("localhost", 8001, Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("Connection timed out"));

        var healthChecker = new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);

        await healthChecker.HealthcheckAllBackendServers(CancellationToken.None);

        backendServerRegister.Received(1).RecordBackendServerHealth(server, false);
    }

    [Test]
    public async Task HealthcheckAllBackendServers_WithConnectionFailure_RecordsUnhealthyStatus()
    {
        var server = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { server });
        tcpClientFactory.CreateAndConnect("localhost", 8001, Arg.Any<CancellationToken>())
            .Throws(new Exception("Connection failed"));

        var healthChecker = new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);

        await healthChecker.HealthcheckAllBackendServers(CancellationToken.None);

        backendServerRegister.Received(1).RecordBackendServerHealth(server, false);
    }

    [Test]
    public async Task HealthcheckAllBackendServers_WithOperationCanceled_DoesNotRecordHealth()
    {
        var server = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { server });
        tcpClientFactory.CreateAndConnect("localhost", 8001, Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var healthChecker = new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);

        await healthChecker.HealthcheckAllBackendServers(CancellationToken.None);

        backendServerRegister.DidNotReceive().RecordBackendServerHealth(Arg.Any<BackendServer>(), Arg.Any<bool>());
    }

    [Test]
    public async Task HealthcheckAllBackendServers_WithMixedResults_RecordsCorrectly()
    {
        var healthyServer = new BackendServer { Host = "healthy", Port = 8001 };
        var unhealthyServer = new BackendServer { Host = "unhealthy", Port = 8002 };
        var timedOutServer = new BackendServer { Host = "timeout", Port = 8003 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { healthyServer, unhealthyServer, timedOutServer });

        var mockClient = Substitute.For<ITcpClientFacade>();
        tcpClientFactory.CreateAndConnect("healthy", 8001, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient));
        tcpClientFactory.CreateAndConnect("unhealthy", 8002, Arg.Any<CancellationToken>())
            .Throws(new Exception("Connection failed"));
        tcpClientFactory.CreateAndConnect("timeout", 8003, Arg.Any<CancellationToken>())
            .Throws(new TimeoutException());

        var healthChecker = new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);

        await healthChecker.HealthcheckAllBackendServers(CancellationToken.None);

        backendServerRegister.Received(1).RecordBackendServerHealth(healthyServer, true);
        backendServerRegister.Received(1).RecordBackendServerHealth(unhealthyServer, false);
        backendServerRegister.Received(1).RecordBackendServerHealth(timedOutServer, false);
    }

    [Test]
    public async Task HealthcheckAllBackendServers_UsesConfiguredTimeout()
    {
        var customSettings = Options.Create(new HealthcheckSettings
        {
            IntervalSeconds = 10,
            TimeoutMilliseconds = 5000
        });
        var server = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { server });

        var tcs = new TaskCompletionSource<ITcpClientFacade>();
        tcpClientFactory.CreateAndConnect("localhost", 8001, Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        var healthChecker = new BackendServersHealthChecker(logger, customSettings, backendServerRegister, tcpClientFactory);

        var healthCheckTask = healthChecker.HealthcheckAllBackendServers(CancellationToken.None);
        await Task.Delay(100);

        Assert.That(healthCheckTask.IsCompleted, Is.False);

        tcs.SetException(new TimeoutException());
        await healthCheckTask;

        backendServerRegister.Received(1).RecordBackendServerHealth(server, false);
    }

    [Test]
    public async Task HealthcheckAllBackendServers_DisposesClientAfterSuccessfulCheck()
    {
        var server = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { server });
        var mockClient = Substitute.For<ITcpClientFacade>();
        tcpClientFactory.CreateAndConnect("localhost", 8001, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient));

        var healthChecker = new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);

        await healthChecker.HealthcheckAllBackendServers(CancellationToken.None);

        mockClient.Received(1).Dispose();
    }

    [Test]
    public async Task HealthcheckAllBackendServers_ChecksAllServersInParallel()
    {
        var server1 = new BackendServer { Host = "server1", Port = 8001 };
        var server2 = new BackendServer { Host = "server2", Port = 8002 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { server1, server2 });

        var tcs1 = new TaskCompletionSource<ITcpClientFacade>();
        var tcs2 = new TaskCompletionSource<ITcpClientFacade>();
        var mockClient1 = Substitute.For<ITcpClientFacade>();
        var mockClient2 = Substitute.For<ITcpClientFacade>();

        tcpClientFactory.CreateAndConnect("server1", 8001, Arg.Any<CancellationToken>())
            .Returns(tcs1.Task);
        tcpClientFactory.CreateAndConnect("server2", 8002, Arg.Any<CancellationToken>())
            .Returns(tcs2.Task);

        var healthChecker = new BackendServersHealthChecker(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);

        var healthCheckTask = healthChecker.HealthcheckAllBackendServers(CancellationToken.None);

        await Task.Delay(50);
        Assert.That(healthCheckTask.IsCompleted, Is.False);

        tcs1.SetResult(mockClient1);
        await Task.Delay(50);
        Assert.That(healthCheckTask.IsCompleted, Is.False);

        tcs2.SetResult(mockClient2);
        await healthCheckTask;

        backendServerRegister.Received(1).RecordBackendServerHealth(server1, true);
        backendServerRegister.Received(1).RecordBackendServerHealth(server2, true);
    }
}
