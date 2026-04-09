using System.Net;
using System.Text;

namespace WpfAutomation.IntegrationTests.TestUtilities;

internal sealed class TestWebServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly Func<HttpListenerContext, Task> _handler;
    private readonly CancellationTokenSource _cts;
    private readonly Task _serveLoop;

    public TestWebServer(Func<HttpListenerContext, Task> handler)
    {
        _handler = handler;
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();

        var port = GetAvailablePort();
        BaseUrl = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();

        _serveLoop = Task.Run(ServeLoopAsync);
    }

    public string BaseUrl { get; }

    private async Task ServeLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => _handler(context));
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();

        try
        {
            await _serveLoop;
        }
        catch
        {
            // Ignore stop race during disposal.
        }

        _cts.Dispose();
    }

    private static int GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static async Task WriteHtmlAsync(HttpListenerResponse response, int statusCode, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }
}
