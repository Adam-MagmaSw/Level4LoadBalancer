using Level4LoadBalancer;
using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.Services;
using Level4LoadBalancer.TcpAbstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReceivedExtensions;

namespace Level4LoadBalancerTests.Unit;

[TestFixture]
public class BackendHealthcheckServiceTests
{
    private ILogger<BackendHealthcheckService> logger;
    private IOptions<HealthcheckSettings> healthcheckSettings;
    private IBackendServerRegister backendServerRegister;
    private ITcpClientFactory tcpClientFactory;
    private BackendHealthcheckService service;

    [SetUp]
    public void SetUp()
    {
        logger = Substitute.For<ILogger<BackendHealthcheckService>>();
        healthcheckSettings = Options.Create(new HealthcheckSettings
        {
            IntervalSeconds = 1,
            TimeoutMilliseconds = 1000
        });
        backendServerRegister = Substitute.For<IBackendServerRegister>();
        tcpClientFactory = Substitute.For<ITcpClientFactory>();
        service = new BackendHealthcheckService(logger, healthcheckSettings, backendServerRegister, tcpClientFactory);
    }

    [TearDown]
    public void TearDown()
    {
        service?.Dispose();
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendHealthcheckService(null!, healthcheckSettings, backendServerRegister, tcpClientFactory));
    }

    [Test]
    public void Constructor_WithNullHealthcheckSettings_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendHealthcheckService(logger, null!, backendServerRegister, tcpClientFactory));
    }

    [Test]
    public void Constructor_WithNullBackendServerRegister_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendHealthcheckService(logger, healthcheckSettings, null!, tcpClientFactory));
    }

    [Test]
    public void Constructor_WithNullTcpClientFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendHealthcheckService(logger, healthcheckSettings, backendServerRegister, null!));
    }

    [Test]
    public async Task ExecuteAsync_WithHealthyBackendServer_RecordsHealthyStatus()
    {
        var backendServer = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { backendServer });

        var mockClient = Substitute.For<ITcpClientFacade>();
        tcpClientFactory.CreateAndConnect(backendServer.Host, backendServer.Port, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient));

        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await Task.Delay(100);
        cts.Cancel();

        await executeTask;

        backendServerRegister.Received().RecordBackendServerHealth(backendServer, true);
    }

    [Test]
    public async Task ExecuteAsync_WithUnhealthyBackendServer_RecordsUnhealthyStatus()
    {
        var backendServer = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { backendServer });

        tcpClientFactory.CreateAndConnect(backendServer.Host, backendServer.Port, Arg.Any<CancellationToken>())
            .Throws(new Exception("Connection failed"));

        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await Task.Delay(100);
        cts.Cancel();

        await executeTask;

        backendServerRegister.Received().RecordBackendServerHealth(backendServer, false);
    }

    [Test]
    public async Task ExecuteAsync_WithTimeoutException_RecordsUnhealthyStatus()
    {
        var backendServer = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { backendServer });

        tcpClientFactory.CreateAndConnect(backendServer.Host, backendServer.Port, Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("Connection timed out"));

        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await Task.Delay(100);
        cts.Cancel();

        await executeTask;

        backendServerRegister.Received().RecordBackendServerHealth(backendServer, false);
    }

    [Test]
    public async Task ExecuteAsync_WithMultipleBackendServers_ChecksAllServers()
    {
        var server1 = new BackendServer { Host = "localhost", Port = 8001 };
        var server2 = new BackendServer { Host = "localhost", Port = 8002 };
        var server3 = new BackendServer { Host = "localhost", Port = 8003 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { server1, server2, server3 });

        var mockClient1 = Substitute.For<ITcpClientFacade>();
        var mockClient2 = Substitute.For<ITcpClientFacade>();
        tcpClientFactory.CreateAndConnect(server1.Host, server1.Port, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient1));
        tcpClientFactory.CreateAndConnect(server2.Host, server2.Port, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient2));
        tcpClientFactory.CreateAndConnect(server3.Host, server3.Port, Arg.Any<CancellationToken>())
            .Throws(new Exception("Connection failed"));

        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await Task.Delay(100);
        cts.Cancel();

        await executeTask;

        backendServerRegister.Received().RecordBackendServerHealth(server1, true);
        backendServerRegister.Received().RecordBackendServerHealth(server2, true);
        backendServerRegister.Received().RecordBackendServerHealth(server3, false);
    }

    [Test]
    public async Task ExecuteAsync_WithOperationCanceledException_DoesNotRecordHealth()
    {
        var backendServer = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { backendServer });

        tcpClientFactory.CreateAndConnect(backendServer.Host, backendServer.Port, Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await Task.Delay(100);
        cts.Cancel();

        await executeTask;

        backendServerRegister.DidNotReceive().RecordBackendServerHealth(backendServer, Arg.Any<bool>());
    }

    [Test]
    public async Task ExecuteAsync_WithNoBackendServers_CompletesWithoutError()
    {
        backendServerRegister.GetAllBackendServers().Returns(Array.Empty<BackendServer>());

        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await Task.Delay(100);
        cts.Cancel();

        await executeTask;

        backendServerRegister.DidNotReceive().RecordBackendServerHealth(Arg.Any<BackendServer>(), Arg.Any<bool>());
    }

    [Test]
    public async Task ExecuteAsync_CancellationRequested_StopsGracefully()
    {
        backendServerRegister.GetAllBackendServers().Returns(Array.Empty<BackendServer>());

        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        await executeTask;

        Assert.Pass();
    }

    [Test]
    public async Task ExecuteAsync_RunsMultipleTimes_ChecksHealthMultipleTimes()
    {
        var backendServer = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { backendServer });

        var mockClient = Substitute.For<ITcpClientFacade>();
        tcpClientFactory.CreateAndConnect(backendServer.Host, backendServer.Port, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient));

        var shortIntervalSettings = Options.Create(new HealthcheckSettings
        {
            IntervalSeconds = 1,
            TimeoutMilliseconds = 1000
        });
        var shortIntervalService = new BackendHealthcheckService(logger, shortIntervalSettings, backendServerRegister, tcpClientFactory);

        var cts = new CancellationTokenSource();
        var executeTask = shortIntervalService.StartAsync(cts.Token);

        await Task.Delay(2000);
        cts.Cancel();

        await executeTask;

        backendServerRegister.Received(Quantity.Within(2, 10)).RecordBackendServerHealth(backendServer, true);
    }

    [Test]
    public async Task ExecuteAsync_DisposesClientAfterCheck()
    {
        var backendServer = new BackendServer { Host = "localhost", Port = 8001 };
        backendServerRegister.GetAllBackendServers().Returns(new[] { backendServer });

        var mockClient = Substitute.For<ITcpClientFacade>();
        tcpClientFactory.CreateAndConnect(backendServer.Host, backendServer.Port, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockClient));

        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await Task.Delay(100);
        cts.Cancel();

        await executeTask;

        mockClient.Received(1).Dispose();
    }
}
