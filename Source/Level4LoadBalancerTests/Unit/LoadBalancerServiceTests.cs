using System.Net;
using Level4LoadBalancer;
using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.LoadBalancingStrategy;
using Level4LoadBalancer.Services;
using Level4LoadBalancer.TcpAbstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Level4LoadBalancerTests.Unit
{
    [TestFixture]
    public class LoadBalancerServiceTests
    {
        private ILogger<LoadBalancerService> logger;
        private IOptions<LoadBalancerSettings> loadBalancerSettings;
        private ITcpListenerFactory tcpListenerFactory;
        private ILoadBalancingStrategy loadBalancingStrategy;
        private ITcpClientFactory tcpClientFactory;
        private IStreamCopier streamCopier;

        [SetUp]
        public void SetUp()
        {
            logger = Substitute.For<ILogger<LoadBalancerService>>();
            loadBalancerSettings = Options.Create(new LoadBalancerSettings());
            tcpListenerFactory = Substitute.For<ITcpListenerFactory>();
            loadBalancingStrategy = Substitute.For<ILoadBalancingStrategy>();
            tcpClientFactory = Substitute.For<ITcpClientFactory>();
            streamCopier = Substitute.For<IStreamCopier>();
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LoadBalancerService(
                null!,
                loadBalancerSettings,
                tcpListenerFactory,
                loadBalancingStrategy,
                tcpClientFactory,
                streamCopier));
        }

        [Test]
        public void Constructor_WithNullLoadBalancerSettings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LoadBalancerService(
                logger,
                null!,
                tcpListenerFactory,
                loadBalancingStrategy,
                tcpClientFactory,
                streamCopier));
        }

        [Test]
        public void Constructor_WithNullTcpListenerFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LoadBalancerService(
                logger,
                loadBalancerSettings,
                null!,
                loadBalancingStrategy,
                tcpClientFactory,
                streamCopier));
        }

        [Test]
        public void Constructor_WithNullLoadBalancingStrategy_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LoadBalancerService(
                logger,
                loadBalancerSettings,
                tcpListenerFactory,
                null!,
                tcpClientFactory,
                streamCopier));
        }

        [Test]
        public void Constructor_WithNullTcpClientFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LoadBalancerService(
                logger,
                loadBalancerSettings,
                tcpListenerFactory,
                loadBalancingStrategy,
                null!,
                streamCopier));
        }

        [Test]
        public void Constructor_WithNullStreamCopier_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LoadBalancerService(
                logger,
                loadBalancerSettings,
                tcpListenerFactory,
                loadBalancingStrategy,
                tcpClientFactory,
                null!));
        }

        [Test]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var service = new LoadBalancerService(
                logger,
                loadBalancerSettings,
                tcpListenerFactory,
                loadBalancingStrategy,
                tcpClientFactory,
                streamCopier);

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public async Task ExecuteAsync_CreatesAndStartsListener()
        {
            var tcpListener = Substitute.For<ITcpListenerFacade>();
            var cts = new CancellationTokenSource();
            
            tcpListenerFactory.CreateAndStart(Arg.Any<IPAddress>(), Arg.Any<int>())
                .Returns(tcpListener);
            
            tcpListener.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    cts.Cancel();
                    return Task.FromCanceled<ITcpClientFacade>(cts.Token);
                });

            var service = new LoadBalancerService(
                logger,
                loadBalancerSettings,
                tcpListenerFactory,
                loadBalancingStrategy,
                tcpClientFactory,
                streamCopier);

            try
            {
                await service.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            await Task.Delay(100);

            tcpListenerFactory.Received(1).CreateAndStart(IPAddress.Any, Arg.Any<int>());
        }

        [Test]
        public async Task ExecuteAsync_AcceptsIncomingConnections()
        {
            var tcpListener = Substitute.For<ITcpListenerFacade>();
            var incomingClient = Substitute.For<ITcpClientFacade>();
            var backendClient = Substitute.For<ITcpClientFacade>();
            var clientStream = Substitute.For<Stream>();
            var backendStream = Substitute.For<Stream>();
            var cts = new CancellationTokenSource();
            var backend = new BackendServer { Host = "localhost", Port = 8080 };

            tcpListenerFactory.CreateAndStart(Arg.Any<IPAddress>(), Arg.Any<int>())
                .Returns(tcpListener);

            var acceptCallCount = 0;
            tcpListener.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    acceptCallCount++;
                    if (acceptCallCount == 1)
                    {
                        return Task.FromResult(incomingClient);
                    }
                    cts.Cancel();
                    return Task.FromCanceled<ITcpClientFacade>(cts.Token);
                });

            loadBalancingStrategy.GetNextBackendServer().Returns(backend);
            
            incomingClient.GetStream().Returns(clientStream);
            backendClient.GetStream().Returns(backendStream);
            
            tcpClientFactory.CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>())
                .Returns(backendClient);

            var service = new LoadBalancerService(
                logger,
                loadBalancerSettings,
                tcpListenerFactory,
                loadBalancingStrategy,
                tcpClientFactory,
                streamCopier);

            try
            {
                await service.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            await Task.Delay(200);

            await tcpListener.Received().AcceptTcpClientAsync(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ConnectIncomingRequestToBackendServer_WithNoAvailableBackend_LogsError()
        {
            var tcpListener = Substitute.For<ITcpListenerFacade>();
            var incomingClient = Substitute.For<ITcpClientFacade>();
            var cts = new CancellationTokenSource();

            tcpListenerFactory.CreateAndStart(Arg.Any<IPAddress>(), Arg.Any<int>())
                .Returns(tcpListener);

            var acceptCallCount = 0;
            tcpListener.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    acceptCallCount++;
                    if (acceptCallCount == 1)
                    {
                        return Task.FromResult(incomingClient);
                    }
                    cts.Cancel();
                    return Task.FromCanceled<ITcpClientFacade>(cts.Token);
                });

            loadBalancingStrategy.GetNextBackendServer().Returns((BackendServer?)null);

            var service = new LoadBalancerService(
                logger,
                loadBalancerSettings,
                tcpListenerFactory,
                loadBalancingStrategy,
                tcpClientFactory,
                streamCopier);

            try
            {
                await service.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            await Task.Delay(200);

            logger.Received().LogError("No backend servers are available to handle the request.");
        }

        [Test]
        public async Task ConnectIncomingRequestToBackendServer_WithAvailableBackend_ConnectsToBackend()
        {
            var tcpListener = Substitute.For<ITcpListenerFacade>();
            var incomingClient = Substitute.For<ITcpClientFacade>();
            var backendClient = Substitute.For<ITcpClientFacade>();
            var clientStream = Substitute.For<Stream>();
            var backendStream = Substitute.For<Stream>();
            var cts = new CancellationTokenSource();
            var backend = new BackendServer { Host = "localhost", Port = 8080 };

            tcpListenerFactory.CreateAndStart(Arg.Any<IPAddress>(), Arg.Any<int>())
                .Returns(tcpListener);

            var acceptCallCount = 0;
            tcpListener.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    acceptCallCount++;
                    if (acceptCallCount == 1)
                    {
                        return Task.FromResult(incomingClient);
                    }
                    cts.Cancel();
                    return Task.FromCanceled<ITcpClientFacade>(cts.Token);
                });

            loadBalancingStrategy.GetNextBackendServer().Returns(backend);
            
            incomingClient.GetStream().Returns(clientStream);
            backendClient.GetStream().Returns(backendStream);
            
            tcpClientFactory.CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>())
                .Returns(backendClient);

            var service = new LoadBalancerService(
                logger,
                loadBalancerSettings,
                tcpListenerFactory,
                loadBalancingStrategy,
                tcpClientFactory,
                streamCopier);

            try
            {
                await service.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            await Task.Delay(200);

            await tcpClientFactory.Received().CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ConnectIncomingRequestToBackendServer_WithAvailableBackend_CopiesStreams()
        {
            var tcpListener = Substitute.For<ITcpListenerFacade>();
            var incomingClient = Substitute.For<ITcpClientFacade>();
            var backendClient = Substitute.For<ITcpClientFacade>();
            var clientStream = Substitute.For<Stream>();
            var backendStream = Substitute.For<Stream>();
            var cts = new CancellationTokenSource();
            var backend = new BackendServer { Host = "localhost", Port = 8080 };

            tcpListenerFactory.CreateAndStart(Arg.Any<IPAddress>(), Arg.Any<int>())
                .Returns(tcpListener);

            var acceptCallCount = 0;
            tcpListener.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    acceptCallCount++;
                    if (acceptCallCount == 1)
                    {
                        return Task.FromResult(incomingClient);
                    }
                    cts.Cancel();
                    return Task.FromCanceled<ITcpClientFacade>(cts.Token);
                });

            loadBalancingStrategy.GetNextBackendServer().Returns(backend);
            
            incomingClient.GetStream().Returns(clientStream);
            backendClient.GetStream().Returns(backendStream);
            
            tcpClientFactory.CreateAndConnect(backend.Host, backend.Port, Arg.Any<CancellationToken>())
                .Returns(backendClient);

            var service = new LoadBalancerService(
                logger,
                loadBalancerSettings,
                tcpListenerFactory,
                loadBalancingStrategy,
                tcpClientFactory,
                streamCopier);

            await service.StartAsync(cts.Token);

            await Task.Delay(200);

            await streamCopier.Received().CopyAsync(clientStream, backendStream, Arg.Any<CancellationToken>());
        }
    }
}
