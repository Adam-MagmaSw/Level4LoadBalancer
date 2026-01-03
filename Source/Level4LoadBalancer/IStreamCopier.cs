
namespace Level4LoadBalancer;

public interface IStreamCopier
{
    Task CopyAsync(Stream clientStream, Stream backendStream, CancellationToken cancellationToken);
}