using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.LoadBalancing;
using Level4LoadBalancer.TcpAbstractions;
using Level4LoadBalancer;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Level4LoadBalancerTests.Unit;

[TestFixture]
public class BackendServerProxyTests
{
    private ILogger<BackendServerProxy> logger;
    private ILoadBalancingStrategy loadBalancingStrategy;
    private ITcpClientFactory tcpClientFactory;
    private IStreamCopier streamCopier;
    private ITcpClientFacade clientFacade;
    private ITcpClientFacade backendClientFacade;
    private Stream clientStream;
    private Stream backendStream;

    [SetUp]
    public void SetUp()
    {
        logger = Substitute.For<ILogger<BackendServerProxy>>();
        loadBalancingStrategy = Substitute.For<ILoadBalancingStrategy>();
        tcpClientFactory = Substitute.For<ITcpClientFactory>();
        streamCopier = Substitute.For<IStreamCopier>();
        clientFacade = Substitute.For<ITcpClientFacade>();
        backendClientFacade = Substitute.For<ITcpClientFacade>();
        clientStream = new MemoryStream();
        backendStream = new MemoryStream();

        clientFacade.GetStream().Returns(clientStream);
        backendClientFacade.GetStream().Returns(backendStream);
    }

    [TearDown]
    public void TearDown()
    {
        clientStream?.Dispose();
        backendStream?.Dispose();
        clientFacade?.Dispose();
        backendClientFacade?.Dispose();
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendServerProxy(null!, loadBalancingStrategy, tcpClientFactory, streamCopier));
    }

    [Test]
    public void Constructor_WithNullLoadBalancingStrategy_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendServerProxy(logger, null!, tcpClientFactory, streamCopier));
    }

    [Test]
    public void Constructor_WithNullTcpClientFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendServerProxy(logger, loadBalancingStrategy, null!, streamCopier));
    }

    [Test]
    public void Constructor_WithNullStreamCopier_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, null!));
    }

    [Test]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);

        Assert.That(proxy, Is.Not.Null);
    }

    [Test]
    public async Task ProxyIncomingConnectionToBackendServer_WithNoAvailableBackends_LogsErrorAndReturns()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);
        loadBalancingStrategy.GetNextBackendServer().Returns((BackendServer?)null);
        var cts = new CancellationTokenSource();

        await proxy.ProxyIncomingConnectionToBackendServer(clientFacade, cts.Token);

        logger.Received(1).LogError("No backend servers are available to handle the request.");
        await tcpClientFactory.DidNotReceive().CreateAndConnect(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await streamCopier.DidNotReceive().CopyAsync(Arg.Any<Stream>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProxyIncomingConnectionToBackendServer_WithAvailableBackend_CreatesConnectionToBackend()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);
        var backend = new BackendServer { Host = "localhost", Port = 8001 };
        loadBalancingStrategy.GetNextBackendServer().Returns(backend);
        tcpClientFactory.CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>())
            .Returns(backendClientFacade);
        var cts = new CancellationTokenSource();

        await proxy.ProxyIncomingConnectionToBackendServer(clientFacade, cts.Token);

        await tcpClientFactory.Received(1).CreateAndConnect(backend.Host, backend.Port, cts.Token);
    }

    [Test]
    public async Task ProxyIncomingConnectionToBackendServer_WithAvailableBackend_CopiesStreamsBetweenClientAndBackend()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);
        var backend = new BackendServer { Host = "localhost", Port = 8001 };
        loadBalancingStrategy.GetNextBackendServer().Returns(backend);
        tcpClientFactory.CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>())
            .Returns(backendClientFacade);
        var cts = new CancellationTokenSource();

        await proxy.ProxyIncomingConnectionToBackendServer(clientFacade, cts.Token);

        await streamCopier.Received(1).CopyAsync(clientStream, backendStream, cts.Token);
    }

    [Test]
    public async Task ProxyIncomingConnectionToBackendServer_WithAvailableBackend_DisposesClientAfterCompletion()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);
        var backend = new BackendServer { Host = "localhost", Port = 8001 };
        loadBalancingStrategy.GetNextBackendServer().Returns(backend);
        tcpClientFactory.CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>())
            .Returns(backendClientFacade);
        var cts = new CancellationTokenSource();

        await proxy.ProxyIncomingConnectionToBackendServer(clientFacade, cts.Token);

        clientFacade.Received(1).Dispose();
    }

    [Test]
    public async Task ProxyIncomingConnectionToBackendServer_WithAvailableBackend_DisposesBackendClient()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);
        var backend = new BackendServer { Host = "localhost", Port = 8001 };
        loadBalancingStrategy.GetNextBackendServer().Returns(backend);
        tcpClientFactory.CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>())
            .Returns(backendClientFacade);
        var cts = new CancellationTokenSource();

        await proxy.ProxyIncomingConnectionToBackendServer(clientFacade, cts.Token);

        backendClientFacade.Received(1).Dispose();
    }

    [Test]
    public async Task ProxyIncomingConnectionToBackendServer_WhenOperationCanceled_DisposesClient()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);
        var backend = new BackendServer { Host = "localhost", Port = 8001 };
        loadBalancingStrategy.GetNextBackendServer().Returns(backend);
        tcpClientFactory.CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());
        var cts = new CancellationTokenSource();

        await proxy.ProxyIncomingConnectionToBackendServer(clientFacade, cts.Token);

        clientFacade.Received(1).Dispose();
    }

    [Test]
    public async Task ProxyIncomingConnectionToBackendServer_WhenExceptionOccurs_LogsErrorAndDisposesClient()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);
        var backend = new BackendServer { Host = "localhost", Port = 8001 };
        var exception = new Exception("Connection failed");
        loadBalancingStrategy.GetNextBackendServer().Returns(backend);
        tcpClientFactory.CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>())
            .Throws(exception);
        var cts = new CancellationTokenSource();

        await proxy.ProxyIncomingConnectionToBackendServer(clientFacade, cts.Token);

        logger.Received(1).LogError(exception, "An error occurred in LoadBalancerService while processing incoming request.");
        clientFacade.Received(1).Dispose();
    }

    [Test]
    public async Task ProxyIncomingConnectionToBackendServer_WhenStreamCopyThrowsException_LogsErrorAndDisposesResources()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);
        var backend = new BackendServer { Host = "localhost", Port = 8001 };
        var exception = new Exception("Stream copy failed");
        loadBalancingStrategy.GetNextBackendServer().Returns(backend);
        tcpClientFactory.CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>())
            .Returns(backendClientFacade);
        streamCopier.CopyAsync(Arg.Any<Stream>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Throws(exception);
        var cts = new CancellationTokenSource();

        await proxy.ProxyIncomingConnectionToBackendServer(clientFacade, cts.Token);

        logger.Received(1).LogError(exception, "An error occurred in LoadBalancerService while processing incoming request.");
        clientFacade.Received(1).Dispose();
        backendClientFacade.Received(1).Dispose();
    }

    [Test]
    public async Task ProxyIncomingConnectionToBackendServer_WithMultipleCalls_SelectsBackendForEachCall()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);
        var backend1 = new BackendServer { Host = "localhost", Port = 8001 };
        var backend2 = new BackendServer { Host = "localhost", Port = 8002 };
        
        loadBalancingStrategy.GetNextBackendServer().Returns(backend1, backend2);
        
        var backendClient1 = Substitute.For<ITcpClientFacade>();
        var backendClient2 = Substitute.For<ITcpClientFacade>();
        var backendStream1 = new MemoryStream();
        var backendStream2 = new MemoryStream();
        backendClient1.GetStream().Returns(backendStream1);
        backendClient2.GetStream().Returns(backendStream2);
        
        tcpClientFactory.CreateAndConnect(backend1.Host, backend1.Port, Arg.Any<CancellationToken>())
            .Returns(backendClient1);
        tcpClientFactory.CreateAndConnect(backend2.Host, backend2.Port, Arg.Any<CancellationToken>())
            .Returns(backendClient2);
        
        var cts = new CancellationTokenSource();
        var client1 = Substitute.For<ITcpClientFacade>();
        var client2 = Substitute.For<ITcpClientFacade>();
        var clientStream1 = new MemoryStream();
        var clientStream2 = new MemoryStream();
        client1.GetStream().Returns(clientStream1);
        client2.GetStream().Returns(clientStream2);

        await proxy.ProxyIncomingConnectionToBackendServer(client1, cts.Token);
        await proxy.ProxyIncomingConnectionToBackendServer(client2, cts.Token);

        loadBalancingStrategy.Received(2).GetNextBackendServer();
        await tcpClientFactory.Received(1).CreateAndConnect(backend1.Host, backend1.Port, Arg.Any<CancellationToken>());
        await tcpClientFactory.Received(1).CreateAndConnect(backend2.Host, backend2.Port, Arg.Any<CancellationToken>());
        
        backendStream1.Dispose();
        backendStream2.Dispose();
        clientStream1.Dispose();
        clientStream2.Dispose();
    }

    [Test]
    public async Task ProxyIncomingConnectionToBackendServer_WhenNoBackendAvailable_DisposesClient()
    {
        var proxy = new BackendServerProxy(logger, loadBalancingStrategy, tcpClientFactory, streamCopier);
        loadBalancingStrategy.GetNextBackendServer().Returns((BackendServer?)null);
        var cts = new CancellationTokenSource();

        await proxy.ProxyIncomingConnectionToBackendServer(clientFacade, cts.Token);

        clientFacade.Received(1).Dispose();
    }
}
