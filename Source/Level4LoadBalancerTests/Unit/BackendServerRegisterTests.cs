using Level4LoadBalancer;
using Level4LoadBalancer.Configuration;
using Microsoft.Extensions.Options;

namespace Level4LoadBalancerTests.Unit;

[TestFixture]
public class BackendServerRegisterTests
{
    [Test]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BackendServerRegister(null!));
    }

    [Test]
    public void Constructor_WithEmptyList_CreatesEmptyRegister()
    {
        var options = Options.Create(new List<BackendServer>());

        var register = new BackendServerRegister(options);

        Assert.That(register.GetAllBackendServers(), Is.Empty);
        Assert.That(register.GetAllHealthyBackendServers(), Is.Empty);
    }

    [Test]
    public void Constructor_WithBackendServers_InitializesAllAsHealthy()
    {
        var backendServers = new List<BackendServer>
        {
            new BackendServer { Host = "localhost", Port = 8001 },
            new BackendServer { Host = "localhost", Port = 8002 },
            new BackendServer { Host = "localhost", Port = 8003 }
        };
        var options = Options.Create(backendServers);

        var register = new BackendServerRegister(options);

        var allServers = register.GetAllBackendServers().ToList();
        var healthyServers = register.GetAllHealthyBackendServers().ToList();
        Assert.That(allServers, Has.Count.EqualTo(3));
        Assert.That(healthyServers, Has.Count.EqualTo(3));
        Assert.That(healthyServers, Is.EquivalentTo(allServers));
    }

    [Test]
    public void GetAllBackendServers_ReturnsAllServers()
    {
        var backendServers = new List<BackendServer>
        {
            new BackendServer { Host = "server1", Port = 8001 },
            new BackendServer { Host = "server2", Port = 8002 }
        };
        var options = Options.Create(backendServers);
        var register = new BackendServerRegister(options);
        register.RecordBackendServerHealth(backendServers[0], false);

        var result = register.GetAllBackendServers().ToList();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result, Is.EquivalentTo(backendServers));
    }

    [Test]
    public void GetAllHealthyBackendServers_ReturnsOnlyHealthyServers()
    {
        var server1 = new BackendServer { Host = "server1", Port = 8001 };
        var server2 = new BackendServer { Host = "server2", Port = 8002 };
        var server3 = new BackendServer { Host = "server3", Port = 8003 };
        
        var backendServers = new List<BackendServer> { server1, server2, server3 };
        var options = Options.Create(backendServers);
        var register = new BackendServerRegister(options);

        register.RecordBackendServerHealth(server2, false);

        var healthyServers = register.GetAllHealthyBackendServers();

        Assert.That(healthyServers, Has.Count.EqualTo(2));
        Assert.That(healthyServers, Does.Contain(server1));
        Assert.That(healthyServers, Does.Not.Contain(server2));
        Assert.That(healthyServers, Does.Contain(server3));
    }

    [Test]
    public void RecordBackendServerHealth_MarkUnhealthyServerAsHealthy_AddsBackToHealthyList()
    {
        var server = new BackendServer { Host = "localhost", Port = 8001 };
        var backendServers = new List<BackendServer> { server };
        var options = Options.Create(backendServers);
        var register = new BackendServerRegister(options);

        register.RecordBackendServerHealth(server, false);
        Assert.That(register.GetAllHealthyBackendServers(), Is.Empty);

        register.RecordBackendServerHealth(server, true);

        var healthyServers = register.GetAllHealthyBackendServers();
        Assert.That(healthyServers, Has.Count.EqualTo(1));
        Assert.That(healthyServers[0], Is.EqualTo(server));
    }
}
