using Level4LoadBalancer;
using Level4LoadBalancer.Configuration;
using Level4LoadBalancer.LoadBalancingStrategy;
using Level4LoadBalancer.Services;
using Level4LoadBalancer.TcpAbstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Collections.Concurrent;

namespace Level4LoadBalancerTests.EndToEnd;

[TestFixture]
public class Level4LoadBalancerTests
{
    private CancellationTokenSource cancellationTokenSource;
    private CancellationTokenSource backEndServer1CancellationTokenSource;
    private SimpleWebServer backEndServer1;
    private SimpleWebServer backEndServer2;
    private SimpleWebServer backEndServer3;
    private Task backEndServer1task;
    private Task backEndServer2task;
    private Task backEndServer3task;
    private LoadBalancerService loadBalancerService;
    private BackendHealthcheckService healthCheckService;
    private ConcurrentStack<string> receivedResponses;

    [SetUp]
    public void SetUp()
    {
        this.receivedResponses = new ConcurrentStack<string>();
        this.cancellationTokenSource = new CancellationTokenSource();
        this.backEndServer1CancellationTokenSource = new CancellationTokenSource();

        this.backEndServer1 = new SimpleWebServer(8080, "Backend Server Port 8080");
        this.backEndServer2 = new SimpleWebServer(8081, "Backend Server Port 8081");
        this.backEndServer3 = new SimpleWebServer(8082, "Backend Server Port 8082");
        this.backEndServer1task = this.backEndServer1.Start(this.backEndServer1CancellationTokenSource.Token);
        this.backEndServer2task = this.backEndServer2.Start(this.cancellationTokenSource.Token);
        this.backEndServer3task = this.backEndServer3.Start(this.cancellationTokenSource.Token);

        var backendServers = new List<BackendServer>
        {
            new BackendServer { Host = "localhost", Port = 8080 },
            new BackendServer { Host = "localhost", Port = 8081 },
            new BackendServer { Host = "localhost", Port = 8082 }
        };
        var options = Options.Create(backendServers);

        var register = new BackendServerRegister(options);

        this.loadBalancerService = new LoadBalancerService(
            Substitute.For<ILogger<LoadBalancerService>>(),
            Options.Create(new LoadBalancerSettings { Port = 80 }),
            new TcpListenerFactory(),
            new RandomLoadBalancingStrategy(register),
            new TcpClientFactory(),
            new StreamCopier());

        this.loadBalancerService.StartAsync(this.cancellationTokenSource.Token);

        this.healthCheckService = new BackendHealthcheckService(
            Substitute.For<ILogger<BackendHealthcheckService>>(),
            Options.Create(new HealthcheckSettings { IntervalSeconds = 1, TimeoutMilliseconds = 50 }),
            register,
            new TcpClientFactory());

        this.healthCheckService.StartAsync(this.cancellationTokenSource.Token);
    }

    [TearDown]
    public async Task TearDown()
    {
        this.cancellationTokenSource.Cancel();
        this.backEndServer1CancellationTokenSource.Cancel();
        this.cancellationTokenSource.Dispose();
        this.backEndServer1CancellationTokenSource.Dispose();
        this.loadBalancerService.Dispose();
        this.healthCheckService.Dispose();

        while(!this.backEndServer1task.IsCompleted &&
            !this.backEndServer2task.IsCompleted &&
            !this.backEndServer3task.IsCompleted)
        {
            await Task.Delay(100);
        }
    }

    [Test]
    public async Task AllBackendServersAreUtilised()
    {
        var client1Task = MakeRequests("Client 1");
        var client2Task = MakeRequests("Client 2");

        await Task.WhenAll(client1Task, client2Task);

        Assert.That(receivedResponses.Count, Is.EqualTo(20));
        Assert.That(receivedResponses.Take(20).Distinct(), Is.EquivalentTo(new[]
        {
            "Backend Server Port 8080",
            "Backend Server Port 8081",
            "Backend Server Port 8082"
        }));
    }

    [Test]
    public async Task UnhealthyServersAreNotUtilised()
    {
        backEndServer1CancellationTokenSource.Cancel();

        await Task.Delay(5000);

        var client1Task = MakeRequests("Client 1");
        var client2Task = MakeRequests("Client 2");

        await Task.WhenAll(client1Task, client2Task);

        Assert.That(receivedResponses.Count, Is.EqualTo(20));
        Assert.That(receivedResponses.Take(20).Distinct(), Is.EquivalentTo(new[]
        {
            "Backend Server Port 8081",
            "Backend Server Port 8082"
        }));
    }

    private async Task MakeRequests(string clientId)
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                using HttpClient client = new HttpClient();
                string url = "http://localhost:80";

                Console.WriteLine($"{DateTime.UtcNow.ToString("O")} : {clientId} : making request");
                string response = await client.GetStringAsync(url);
                Console.WriteLine($"{DateTime.UtcNow.ToString("O")} : {clientId} : received response: {response}");
                receivedResponses.Push(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.UtcNow.ToString("O")} : {clientId} : request failed: {ex.Message}");
            }
        }
    }
}
