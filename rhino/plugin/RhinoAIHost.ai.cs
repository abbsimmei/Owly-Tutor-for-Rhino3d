
namespace Rhino.AI;

internal static class RhinoAIHost
{

    private static Dictionary<uint, McpServer> Servers { get; } = new();

    // Re-bound by the replacing document, so a swap doesn't strand clients on a fixed port.
    private static int? PortFreedByLastClose { get; set; }

    // Once any server has run this session, MCP is wanted even with auto-load off, so new documents get one too.
    private static bool HasStartedThisSession { get; set; }

    private static bool McpRequested => HasStartedThisSession || AIAutoLoad.ShouldAutoLoad();

    private static void OpenServer(object? sender, DocumentOpenEventArgs e)
    {
        if (e.Merge)
            return;

        if (e.Reference)
            return;

        OpenNewServer(sender, e);
    }

    private static void OpenNewServer(object? sender, DocumentEventArgs e)
    {
        if (e.Document is null)
            return;

        if (HasStarted(e.Document))
            return;

        if (!McpRequested)
            return;

        int? wanted = PortFreedByLastClose;
        if (!TryGetReplacementPort(out int port))
        {
            RhinoApp.WriteLine("[RhinoAI] No free port available; the MCP server did not restart.");
            return;
        }

        if (!Start(e.Document, port))
        {
            RhinoApp.WriteLine($"[RhinoAI] The MCP server failed to restart on port {port}.");
            return;
        }

        if (wanted is int previous && previous != port)
            RhinoApp.WriteLine($"[RhinoAI] Port {previous} was unavailable; MCP server moved to http://localhost:{port}/");
    }

    private static bool TryGetReplacementPort(out int port)
    {
        int? recycled = PortFreedByLastClose;
        PortFreedByLastClose = null;

        if (recycled is int candidate && TryBindCandidate(candidate, out port))
            return true;

        return TryGetNextPort(out port);
    }

    // A doc that owned a listener is closing (File>New/Open, or a plain close). 
    // Stop the server, otherwise the listener is orphaned.
    private static void CloseServer(object? sender, DocumentEventArgs e)
    {
        if (!Servers.Remove(e.DocumentSerialNumber, out McpServer? server))
            return;

        if (server is not null)
        {
            PortFreedByLastClose = server.Port;
            server.Stop();
        }

    }

    public static bool HasStarted(RhinoDoc doc) =>
        Servers.TryGetValue(doc.RuntimeSerialNumber, out McpServer? server)
            && (server?.HasStarted ?? false);

    public static bool TryGetPortFor(RhinoDoc doc, out int port)
    {
        port = -1;
        if (!Servers.TryGetValue(doc.RuntimeSerialNumber, out McpServer? server))
            return false;
        if (!server.HasStarted)
            return false;
        port = server.Port;
        return true;
    }

    private const int DefaultPort = 10500;

    // Worked-or-not: true with a bound-then-released free port in `port`, false if the
    // OS could give us nothing. We bind the candidate to port 0 so the OS assigns any
    // free ephemeral port rather than failing when our preferred Max+1 happens to be
    // occupied.
    public static bool TryGetNextPort(out int port)
    {
        int candidate = DefaultPort;
        if (Servers.Count > 0)
        {
            candidate = Servers.Max(s => s.Value.Port) + 1;
        }

        if (TryBindCandidate(candidate, out port))
            return true;

        return TryBindCandidate(0, out port);
    }

    private static bool TryBindCandidate(int candidate, out int port)
    {
        port = default;
        try
        {
            System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, candidate);
            listener.Start();
            port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    public static bool Start(RhinoDoc doc, int port)
    {
        if (TryGetPortFor(doc, out int existingPort))
            return existingPort == port;

        McpServer server = new();
        Servers[doc.RuntimeSerialNumber] = server;

        bool ok = server.Start(doc, port);
        if (ok)
        {
            HasStartedThisSession = true;
            return true;
        }

        // A failed start must not leave a dead entry (Port set, HasStarted false) behind.
        Servers.Remove(doc.RuntimeSerialNumber);
        return false;
    }

    public static void Stop(RhinoDoc doc)
    {
        if (!Servers.Remove(doc.RuntimeSerialNumber, out McpServer? server))
            return;
        if (server is not null)
        {
            server.Stop();
        }
    }

    public static bool RestartOnPort(RhinoDoc doc, int port)
    {
        if (port < 1 || port > 65535)
            return false;
        Stop(doc);
        return Start(doc, port);
    }

    // Shared start/restart dispatch. Writes user-facing status lines.
    public static bool StartOrRestart(RhinoDoc doc, int port, bool quiet = false)
    {
        if (HasStarted(doc))
        {
            if (!RestartOnPort(doc, port))
            {
                if (!quiet)
                {
                    RhinoApp.WriteLine($"[RhinoAI] Failed to bind port {port}.");
                }
                return false;
            }
            if (!quiet)
            {
                RhinoApp.WriteLine($"[RhinoAI] Restarted on http://localhost:{port}/");
            }
            return true;
        }

        if (Start(doc, port))
            return true;

        if (!quiet)
        {
            RhinoApp.WriteLine($"[RhinoAI] MCP server failed to start. Try a different port.");
        }
        return false;
    }

    internal static void RegisterDocumentWatcher()
    {
        RhinoDoc.CloseDocument += CloseServer;
        RhinoDoc.NewDocument += OpenNewServer;
        RhinoDoc.EndOpenDocument += OpenServer;
    }

}
