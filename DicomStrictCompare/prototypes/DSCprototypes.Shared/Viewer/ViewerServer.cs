using System.Net;
using System.Security.Cryptography;

namespace DSCprototypes.Shared.Viewer;

/// <summary>
/// Serves the viewer page, NiiVue and one folder of maps to this machine only (127.0.0.1, a random port and a random
/// path), so a WebView or the system browser can open them; nothing leaves the computer. Dispose to stop.
/// </summary>
public sealed class ViewerServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _assets;
    private readonly string _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
    private volatile string _maps;

    /// <summary>The page's address.</summary>
    public string Url { get; }

    /// <param name="maps">The folder <see cref="DifferenceViewer.Prepare"/> wrote.</param>
    public ViewerServer(string maps)
    {
        _maps = maps;
        _assets = Path.Combine(AppContext.BaseDirectory, "Viewer");
        if (!File.Exists(Path.Combine(_assets, "viewer.html"))) throw new FileNotFoundException("The viewer page isn't beside the app", Path.Combine(_assets, "viewer.html"));
        for (int attempt = 0; ; attempt++)
        {
            int port = RandomNumberGenerator.GetInt32(20000, 60000);
            try
            {
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/{_token}/");
                _listener.Start();
                Url = $"http://127.0.0.1:{port}/{_token}/";
                break;
            }
            catch (HttpListenerException) when (attempt < 10) { }
        }
        _ = Task.Run(Serve);
    }

    /// <summary>Points the server at another folder of maps; the page reloads them.</summary>
    public void Show(string maps) => _maps = maps;

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (Exception) { return; }
            _ = Task.Run(() => Answer(context));
        }
    }

    private void Answer(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            string rest = context.Request.Url!.AbsolutePath[(_token.Length + 2)..];
            string? file = rest switch
            {
                "" or "viewer.html" => Path.Combine(_assets, "viewer.html"),
                "niivue.umd.js" => Path.Combine(_assets, "assets", "niivue.umd.js"),
                _ when rest.StartsWith("maps/", StringComparison.Ordinal) && Path.GetFileName(rest) is var name
                       && name is DifferenceViewer.DoseFile or DifferenceViewer.GammaFile or DifferenceViewer.PercentFile or DifferenceViewer.InfoFile
                    => Path.Combine(_maps, name),
                _ => null,
            };
            if (file is null || !File.Exists(file)) { response.StatusCode = 404; return; }
            response.ContentType = Path.GetExtension(file) switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js" => "text/javascript; charset=utf-8",
                ".json" => "application/json",
                _ => "application/octet-stream",
            };
            response.Headers["Cache-Control"] = "no-store";
            using var input = File.OpenRead(file);
            response.ContentLength64 = input.Length;
            input.CopyTo(response.OutputStream);
        }
        catch (Exception) { response.StatusCode = 500; }
        finally { try { response.Close(); } catch (Exception) { } }
    }

    public void Dispose()
    {
        try { _listener.Stop(); _listener.Close(); } catch (ObjectDisposedException) { }
    }
}
