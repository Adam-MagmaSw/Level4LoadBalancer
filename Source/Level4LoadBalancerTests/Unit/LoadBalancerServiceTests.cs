using System.Net;
using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.LoadBalancing;
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
        private IBackendServerProxy backendServerProxy;
        private ITcpListenerFacade tcpListenerFacade;
        private ITcpClientFacade tcpClientFacade;

        [SetUp]
        public void SetUp()
        {
            logger = Substitute.For<ILogger<LoadBalancerService>>();
            loadBalancerSettings = Options.Create(new LoadBalancerSettings
            {
                Port = 8080
            });
            tcpListenerFactory = Substitute.For<ITcpListenerFactory>();
            backendServerProxy = Substitute.For<IBackendServerProxy>();
            tcpListenerFacade = Substitute.For<ITcpListenerFacade>();
            tcpClientFacade = Substitute.For<ITcpClientFacade>();

            tcpListenerFactory.CreateAndStart(Arg.Any<IPAddress>(), Arg.Any<int>())
                .Returns(tcpListenerFacade);
        }

        [TearDown]
        public void TearDown()
        {
            tcpListenerFacade?.Dispose();
            tcpClientFacade?.Dispose();
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LoadBalancerService(null!, loadBalancerSettings, tcpListenerFactory, backendServerProxy));
        }

        [Test]
        public void Constructor_WithNullLoadBalancerSettings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LoadBalancerService(logger, null!, tcpListenerFactory, backendServerProxy));
        }

        [Test]
        public void Constructor_WithNullTcpListenerFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LoadBalancerService(logger, loadBalancerSettings, null!, backendServerProxy));
        }

        [Test]
        public void Constructor_WithNullBackendServerProxy_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LoadBalancerService(logger, loadBalancerSettings, tcpListenerFactory, null!));
        }

        [Test]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var service = new LoadBalancerService(logger, loadBalancerSettings, tcpListenerFactory, backendServerProxy);

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public async Task ExecuteAsync_CreatesTcpListener_WithCorrectParameters()
        {
            var service = new LoadBalancerService(logger, loadBalancerSettings, tcpListenerFactory, backendServerProxy);
            var cts = new CancellationTokenSource();

            tcpListenerFacade.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromException<ITcpClientFacade>(new OperationCanceledException()));

            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);

            tcpListenerFactory.Received(1).CreateAndStart(IPAddress.Any, 8080);
        }

        [Test]
        public async Task ExecuteAsync_AcceptsIncomingConnections_AndProxiesToBackend()
        {
            var service = new LoadBalancerService(logger, loadBalancerSettings, tcpListenerFactory, backendServerProxy);
            var cts = new CancellationTokenSource();

            var callCount = 0;
            tcpListenerFacade.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return Task.FromResult(tcpClientFacade);
                    }
                    cts.Cancel();
                    return Task.FromException<ITcpClientFacade>(new OperationCanceledException());
                });

            backendServerProxy.ProxyIncomingConnectionToBackendServer(Arg.Any<ITcpClientFacade>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await service.StartAsync(cts.Token);
            await Task.Delay(100);
            await service.StopAsync(CancellationToken.None);

            await backendServerProxy.Received(1).ProxyIncomingConnectionToBackendServer(tcpClientFacade, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ExecuteAsync_AcceptsMultipleConnections_AndProxiesEachToBackend()
        {
            var service = new LoadBalancerService(logger, loadBalancerSettings, tcpListenerFactory, backendServerProxy);
            var cts = new CancellationTokenSource();

            var client1 = Substitute.For<ITcpClientFacade>();
            var client2 = Substitute.For<ITcpClientFacade>();
            var client3 = Substitute.For<ITcpClientFacade>();

            var callCount = 0;
            tcpListenerFacade.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    callCount++;
                    return callCount switch
                    {
                        1 => Task.FromResult(client1),
                        2 => Task.FromResult(client2),
                        3 => Task.FromResult(client3),
                        _ => Task.FromException<ITcpClientFacade>(new OperationCanceledException())
                    };
                });

            var proxyCallCount = 0;
            backendServerProxy.ProxyIncomingConnectionToBackendServer(Arg.Any<ITcpClientFacade>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    proxyCallCount++;
                    if (proxyCallCount >= 3)
                    {
                        cts.Cancel();
                    }
                    return Task.CompletedTask;
                });

            await service.StartAsync(cts.Token);
            await Task.Delay(200);
            await service.StopAsync(CancellationToken.None);

            await backendServerProxy.Received(1).ProxyIncomingConnectionToBackendServer(client1, Arg.Any<CancellationToken>());
            await backendServerProxy.Received(1).ProxyIncomingConnectionToBackendServer(client2, Arg.Any<CancellationToken>());
            await backendServerProxy.Received(1).ProxyIncomingConnectionToBackendServer(client3, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ExecuteAsync_StopsGracefully_WhenCancellationRequested()
        {
            var service = new LoadBalancerService(logger, loadBalancerSettings, tcpListenerFactory, backendServerProxy);
            var cts = new CancellationTokenSource();

            tcpListenerFacade.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var token = callInfo.Arg<CancellationToken>();
                    return token.IsCancellationRequested
                        ? Task.FromException<ITcpClientFacade>(new OperationCanceledException())
                        : Task.FromResult(tcpClientFacade);
                });

            backendServerProxy.ProxyIncomingConnectionToBackendServer(Arg.Any<ITcpClientFacade>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            cts.Cancel();
            await Task.Delay(50);
            await service.StopAsync(CancellationToken.None);

            logger.Received().LogInformation("LoadBalancerService is stopping.");
        }

        [Test]
        public async Task ExecuteAsync_DisposesTcpListener_WhenServiceStops()
        {
            var service = new LoadBalancerService(logger, loadBalancerSettings, tcpListenerFactory, backendServerProxy);
            var cts = new CancellationTokenSource();

            tcpListenerFacade.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromException<ITcpClientFacade>(new OperationCanceledException()));

            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);

            tcpListenerFacade.Received(1).Dispose();
        }

        [Test]
        public async Task ExecuteAsync_DoesNotAwaitProxyTask_AllowsConcurrentConnections()
        {
            var service = new LoadBalancerService(logger, loadBalancerSettings, tcpListenerFactory, backendServerProxy);
            var cts = new CancellationTokenSource();

            var client1 = Substitute.For<ITcpClientFacade>();
            var client2 = Substitute.For<ITcpClientFacade>();

            var acceptCallCount = 0;
            tcpListenerFacade.AcceptTcpClientAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    acceptCallCount++;
                    if (acceptCallCount == 1)
                    {
                        return Task.FromResult(client1);
                    }
                    if (acceptCallCount == 2)
                    {
                        return Task.FromResult(client2);
                    }
                    cts.Cancel();
                    return Task.FromException<ITcpClientFacade>(new OperationCanceledException());
                });

            var proxy1Tcs = new TaskCompletionSource();
            var proxy2Tcs = new TaskCompletionSource();
            var proxyCallCount = 0;

            backendServerProxy.ProxyIncomingConnectionToBackendServer(Arg.Any<ITcpClientFacade>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    proxyCallCount++;
                    if (proxyCallCount == 1)
                    {
                        return proxy1Tcs.Task;
                    }
                    if (proxyCallCount == 2)
                    {
                        proxy1Tcs.SetResult();
                        proxy2Tcs.SetResult();
                        return proxy2Tcs.Task;
                    }
                    return Task.CompletedTask;
                });

            await service.StartAsync(cts.Token);
            await Task.Delay(200);
            await service.StopAsync(CancellationToken.None);

            Assert.That(acceptCallCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(proxyCallCount, Is.GreaterThanOrEqualTo(2));
        }
    }
}
