using System;
using System.Threading;
using System.Threading.Tasks;
using Acp;

namespace Rhino.AI;

// Drives any ACP agent (a StreamJsonAgent wrapping a stream-json
// CLI like Claude) behind the plugin's IAgentRunner seam. One ACP session per agent instance:
// the first prompt initializes the connection and opens a session pointed at this doc's MCP
// server; later prompts reuse it. Streaming arrives out-of-band through RhinoAcpClient on the
// connection's read loop; the prompt response ends the turn.
internal sealed class AgentRunner : IAgentRunner
{
    private AgentDefinition Definition { get; }
    private RhinoAcpClient Client { get; }
    private Func<IAcpClient, Conversation, string, IAcpAgent> Connect { get; }
    private SemaphoreSlim TurnGate { get; } = new(1, 1);
    private object Gate { get; } = new();

    private IAcpAgent? Connection { get; set; }

    // A real token from SessionNewAsync once Started is true, never read before then (PromptAsync
    // goes through EnsureStartedAsync first); the initial empty string is just the pre-start value,
    // not an absence sentinel.
    private string SessionId { get; set; } = string.Empty;

    private bool Started { get; set; }

    public AgentRunner(AgentDefinition def, string docTitle, Func<IAcpClient, Conversation, string, IAcpAgent> connect)
        : this(def, new Conversation(Guid.NewGuid(), def.Name, docTitle), connect)
    {
    }

    // Reopening a saved conversation: drive the restored Conversation (its turns are already shown)
    // rather than a fresh one. The connection factory seeds the CLI's --resume id from it.
    public AgentRunner(AgentDefinition def, Conversation conversation, Func<IAcpClient, Conversation, string, IAcpAgent> connect)
    {
        Definition = def;
        Connect = connect;
        Conversation = conversation;
        Conversation.TitleFallback = Rhino.UI.Localization.LocalizeString("Conversation", 390);
        Client = new RhinoAcpClient(Conversation);
    }

    public string Name => Definition.Name;

    public Conversation Conversation { get; }

    public async Task PromptAsync(UserMessage message, string mcpUrl, string cwd)
    {
        await TurnGate.WaitAsync().ConfigureAwait(false);
        try
        {
            IAcpAgent connection = await EnsureStartedAsync(mcpUrl, cwd).ConfigureAwait(false);
            // Plan 027: a fresh conversation starts with its project's notes, in front of the first message and
            // over stdin like the rest. They are not part of the transcript (BeginTurn keeps only message.Text).
            string notes = Conversation.Turns.Count == 0 && !Conversation.Restored ? StartContext() : string.Empty;
            Conversation.BeginTurn(message.Text, [.. message.Attachments.Select(static a => a.Info)]);
            Acp.ContentBlock[] blocks = AcpMessageMapper.Prompt(message);
            if (notes.Length > 0)
                blocks = [new TextContentBlock { Text = notes + "\n\n" }, .. blocks];
            await connection.SessionPromptAsync(new PromptRequest
            {
                SessionId = SessionId,
                Prompt = blocks,
            }).ConfigureAwait(false);
        }
        finally
        {
            Conversation.CompleteTurn();
            PersistTurn();
            TurnGate.Release();
        }
    }

    // What a fresh conversation starts with: its project's notes and, when main memory is on, the list of what
    // the student finds hard (plan 028). Data, framed as such, never on the command line.
    private string StartContext()
    {
        try
        {
            string project = Conversation.ProjectId ?? ProjectRegistry.UnsortedId;
            string notes = NotesPrompt.ContextBlock(OwlyMemory.Notes.List(project).Select(note => note.Text).ToList());
            string hard = OwlyMemory.Main.Enabled ? NotesPrompt.DifficultiesBlock(OwlyMemory.Main.Items()) : string.Empty;
            if (hard.Length == 0) return notes;
            return notes.Length == 0 ? hard : notes + "\n\n" + hard;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public async Task LoginAsync(string mcpUrl, string cwd)
    {
        if (!Name.Equals("claude", StringComparison.OrdinalIgnoreCase))
        {
            Conversation.NoteSystem($"/login is not supported for {Name}. Sign in using its CLI in a terminal.");
            return;
        }
        if (!await TurnGate.WaitAsync(0).ConfigureAwait(false))
        {
            Conversation.NoteSystem("Stop the running turn before signing in.");
            return;
        }
        try
        {
            IAcpAgent connection = await EnsureStartedAsync(mcpUrl, cwd).ConfigureAwait(false);
            if (connection is StreamJsonAgent cli)
                cli.Login();
        }
        catch (Exception ex)
        {
            Conversation.NoteSystem($"Could not start sign-in: {ex.Message}");
        }
        finally
        {
            TurnGate.Release();
        }
    }

    public void Cancel()
    {
        IAcpAgent? connection;
        string sessionId;
        lock (Gate)
        {
            connection = Connection;
            sessionId = SessionId;
        }
        if (connection is not null)
            _ = connection.SessionCancelAsync(new CancelNotification { SessionId = sessionId });
    }

    public void Dispose()
    {
        IAcpAgent? connection;
        lock (Gate)
            connection = Connection;
        (connection as IDisposable)?.Dispose();
        // TurnGate is deliberately not disposed: a turn cancelled by this teardown still runs its
        // finally (TurnGate.Release), and disposing here would race it into ObjectDisposedException.
    }

    // Saved after every turn, on the turn's background thread, so a crash loses at most the turn in flight.
    private void PersistTurn()
    {
        try
        {
            if (OwlyMemory.Conversations.Save(Conversation))
                OwlyMemory.NotifyConversationsChanged();
        }
        catch (Exception)
        {
            // A disk hiccup must never fault the turn the student just ran.
        }
    }

    private async Task<IAcpAgent> EnsureStartedAsync(string mcpUrl, string cwd)
    {
        if (Started)
        {
            lock (Gate)
                return Connection ?? throw new InvalidOperationException("Connection missing after start.");
        }

        IAcpAgent connection = Connect(Client, Conversation, cwd);
        try
        {
            await connection.InitializeAsync(new InitializeRequest { ProtocolVersion = ProtocolConstants.Version }).ConfigureAwait(false);
            NewSessionResponse session = await connection.SessionNewAsync(new NewSessionRequest
            {
                Cwd = cwd,
                McpServers = [new HttpMcpServer { Name = "rhino", Url = mcpUrl, Headers = [] }],
            }).ConfigureAwait(false);

            lock (Gate)
            {
                Connection = connection;
                SessionId = session.SessionId;
                Started = true;
            }
            Conversation.NoteSessionStarted();
            return connection;
        }
        catch
        {
            (connection as IDisposable)?.Dispose();
            throw;
        }
    }
}
