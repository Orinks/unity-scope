using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityScope.Server;

namespace UnityScope.Transport
{
    internal class HttpTransport : ITransport
    {
        // Default loopback port. Stable across restarts so MCP clients don't have
        // to re-resolve discovery every launch. Overridable via the Port config.
        public const int DefaultPort = 17897;

        private readonly RequestRouter _router;
        private readonly int _preferredPort;
        private HttpListener _listener;
        private Thread _acceptThread;
        private string _authToken;
        private string _endpoint;
        private volatile bool _running;

        public string EndpointDescription => $"{_endpoint} (http, token-protected)";

        public HttpTransport(RequestRouter router) : this(router, DefaultPort) { }

        public HttpTransport(RequestRouter router, int preferredPort)
        {
            _router = router;
            _preferredPort = preferredPort;
        }

        public void Start()
        {
            int port = BindListener(out _listener);
            _endpoint = $"http://127.0.0.1:{port}/";
            _authToken = Guid.NewGuid().ToString("N");

            DiscoveryFile.Write("http", _endpoint.TrimEnd('/'), _authToken);

            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "UnityScope-Http" };
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            DiscoveryFile.Delete();
        }

        // Try the preferred (stable) port first, then fall back to an OS-assigned free port.
        private int BindListener(out HttpListener listener)
        {
            if (_preferredPort > 0 && TryBind(_preferredPort, out listener))
                return _preferredPort;

            int free = ReserveFreePort();
            if (!TryBind(free, out listener))
                throw new InvalidOperationException("Failed to bind HTTP listener on any loopback port.");
            return free;
        }

        private static bool TryBind(int port, out HttpListener listener)
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try { listener.Start(); return true; }
            catch { try { listener.Close(); } catch { } listener = null; return false; }
        }

        // HttpListener can't bind to port 0 directly. Reserve via TcpListener, then release.
        private static int ReserveFreePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { return; }

                ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            try
            {
                if (!string.Equals(ctx.Request.Headers["X-UnityScope-Token"], _authToken, StringComparison.Ordinal))
                {
                    Write(ctx, 401, "{\"error\":\"unauthorized\"}");
                    return;
                }

                string body = null;
                if (ctx.Request.HasEntityBody)
                {
                    using (var sr = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8))
                        body = sr.ReadToEnd();
                }

                var response = _router.Dispatch(
                    ctx.Request.HttpMethod,
                    ctx.Request.Url.AbsolutePath,
                    ctx.Request.QueryString,
                    body);

                Write(ctx, response.Status, response.Body);
            }
            catch (Exception ex)
            {
                try { Write(ctx, 500, $"{{\"error\":\"{Escape(ex.Message)}\"}}"); }
                catch { }
            }
        }

        private static void Write(HttpListenerContext ctx, int status, string body)
        {
            byte[] buf = Encoding.UTF8.GetBytes(body ?? "");
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = buf.Length;
            ctx.Response.OutputStream.Write(buf, 0, buf.Length);
            ctx.Response.OutputStream.Close();
        }

        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
