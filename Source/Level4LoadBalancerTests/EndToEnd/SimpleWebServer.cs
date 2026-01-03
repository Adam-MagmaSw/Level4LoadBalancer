using System.Net;
using System.Text;

namespace Level4LoadBalancerTests.EndToEnd;

internal class SimpleWebServer
{
    private readonly byte[] responseBuffer;
    private readonly int port;
    private HttpListener listener;

    public SimpleWebServer(int port, string response)
    {
        this.listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        this.responseBuffer = Encoding.UTF8.GetBytes(response);
        this.port = port;
    }

    public Task Start(CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            Console.WriteLine($"Simple web server starting on port {this.port}");
            listener.Start();
            Console.WriteLine($"Simple web server started on port {this.port}");

            while (!cancellationToken.IsCancellationRequested)
            {
                var context = listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
                context.AsyncWaitHandle.WaitOne(500, true);
            }

            Console.WriteLine($"Simple web server stopping on port {this.port}");
            listener.Stop();
            listener.Close();
            Console.WriteLine($"Simple web server stopped on port {this.port}");
        }, cancellationToken);
    }

    private void ListenerCallback(IAsyncResult result)
    {
        try
        {
            HttpListener? listener = (HttpListener?)result.AsyncState;
            if (listener == null)
            {
                return;
            }
            HttpListenerContext context = listener.EndGetContext(result);
            var response = context.Response;
            response.ContentLength64 = responseBuffer.Length;
            response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
            response.OutputStream.Close();
        }
        catch { }
    }
}

