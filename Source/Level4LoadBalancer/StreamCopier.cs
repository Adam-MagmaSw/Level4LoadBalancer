namespace Level4LoadBalancer;

public class StreamCopier : IStreamCopier
{
    public async Task CopyAsync(Stream clientStream, Stream backendStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clientStream);
        ArgumentNullException.ThrowIfNull(backendStream);

        var copyClientToBackendTask = clientStream.CopyToAsync(backendStream, cancellationToken);
        var copyBackendToClientTask = backendStream.CopyToAsync(clientStream, cancellationToken);

        await Task.WhenAny(copyClientToBackendTask, copyBackendToClientTask);
    }
}
