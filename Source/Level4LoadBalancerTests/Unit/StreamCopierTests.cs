using Level4LoadBalancer;
using System.Text;

namespace Level4LoadBalancerTests.Unit;

[TestFixture]
public class StreamCopierTests
{
    private StreamCopier streamCopier;

    [SetUp]
    public void SetUp()
    {
        streamCopier = new StreamCopier();
    }

    [Test]
    public void CopyAsync_WithNullClientStream_ThrowsArgumentNullException()
    {
        var backendStream = new MemoryStream();
        var cts = new CancellationTokenSource();

        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await streamCopier.CopyAsync(null!, backendStream, cts.Token));
    }

    [Test]
    public void CopyAsync_WithNullBackendStream_ThrowsArgumentNullException()
    {
        var clientStream = new MemoryStream();
        var cts = new CancellationTokenSource();

        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await streamCopier.CopyAsync(clientStream, null!, cts.Token));
    }

    [Test]
    public async Task CopyAsync_CopiesDataFromClientToBackend()
    {
        const string clientDataString = "Hello from client";
        var clientData = Encoding.UTF8.GetBytes(clientDataString);
        var clientStream = new MemoryStream(clientData);
        var backendStream = new MemoryStream();
        var cts = new CancellationTokenSource();

        await streamCopier.CopyAsync(clientStream, backendStream, cts.Token);

        Assert.That(backendStream.Length, Is.EqualTo(clientDataString.Length));
    }

    [Test]
    public async Task CopyAsync_CopiesDataFromBackendToClient()
    {
        const string backendDataString = "Hello from backend";
        var backendData = Encoding.UTF8.GetBytes(backendDataString);
        var clientStream = new MemoryStream();
        var backendStream = new MemoryStream(backendData);
        var cts = new CancellationTokenSource();

        await streamCopier.CopyAsync(clientStream, backendStream, cts.Token);

        Assert.That(clientStream.Length, Is.EqualTo(backendDataString.Length));
    }

    [Test]
    public async Task CopyAsync_WithEmptyStreams_CompletesWithoutError()
    {
        var clientStream = new MemoryStream();
        var backendStream = new MemoryStream();
        var cts = new CancellationTokenSource();

        await streamCopier.CopyAsync(clientStream, backendStream, cts.Token);

        Assert.Pass();
    }

    [Test]
    public async Task CopyAsync_CompletesWhenEitherStreamCloses()
    {
        var clientData = Encoding.UTF8.GetBytes("Test data");
        var clientStream = new MemoryStream(clientData);
        var backendStream = new MemoryStream();
        var cts = new CancellationTokenSource(200);

        var copyTask = streamCopier.CopyAsync(clientStream, backendStream, cts.Token);

        var completedTask = await Task.WhenAny(copyTask, Task.Delay(300));

        Assert.That(completedTask == copyTask, Is.True);
    }

    [Test]
    public async Task CopyAsync_WithLargeData_CopiesSuccessfully()
    {
        var largeData = new byte[1024 * 100];
        new Random().NextBytes(largeData);

        var clientStream = new MemoryStream(largeData);
        var backendStream = new MemoryStream();
        var cts = new CancellationTokenSource();

        await streamCopier.CopyAsync(clientStream, backendStream, cts.Token);

        Assert.That(backendStream.Length, Is.EqualTo(largeData.Length));
    }
}
