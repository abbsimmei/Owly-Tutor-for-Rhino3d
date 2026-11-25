using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Rhino.Runtime;

namespace Rhino.AI;

// Plan 027 / ADR-0009: when a conversation ends, ONE extra Claude request asks Haiku for 3-5 short notes
// about the project, which ProjectNotes stores. It has no tools, no settings, no MCP server and no session
// file of its own, and the transcript goes in on stdin, never on the command line (cmd.exe limits that to
// 8,191 characters). The job is claimed on disk first (ConversationStore.TryClaimNotes), so two Rhinos
// can't both run it. One job runs at a time, off the UI thread, and a failure is silent: the job goes back
// to pending and the next start tries again. This is the only place --no-session-persistence is used.
internal static class ProjectNotesWriter
{
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);
    private static readonly TimeSpan CliTimeout = TimeSpan.FromSeconds(60);

    // Queues the notes job of a conversation that just ended. Returns at once.
    public static void Enqueue(string projectId, string sessionId) =>
        _ = Task.Run(() => RunJobAsync(projectId, sessionId));

    // Start-up: queues every conversation whose notes are still to be written (Rhino quit, or a failure).
    public static void DrainPending() =>
        _ = Task.Run(() =>
        {
            try
            {
                foreach ((string projectId, string sessionId) in OwlyMemory.Conversations.ListPending())
                    Enqueue(projectId, sessionId);
            }
            catch (Exception ex)
            {
                Log($"could not list the pending notes: {ex.Message}");
            }
        });

    private static async Task RunJobAsync(string projectId, string sessionId)
    {
        await OneAtATime.WaitAsync().ConfigureAwait(false);
        try
        {
            ConversationStore conversations = OwlyMemory.Conversations;
            if (!conversations.TryClaimNotes(projectId, sessionId, out ConversationDto dto)) return;

            List<TurnDto> turns = dto.Turns.Skip(dto.NotesThroughTurn).ToList();
            if (!turns.Any(turn => turn.Events.Any(ev => ev.Kind == TurnEventKind.AssistantText)))
            {
                conversations.FinishNotes(projectId, sessionId, dto.Turns.Count);
                return;
            }

            string[] existing = OwlyMemory.Notes.List(projectId).Select(note => note.Text).ToArray();
            string? answer = await AskAsync(NotesPrompt.Input(existing, turns)).ConfigureAwait(false);
            if (answer is null)
            {
                conversations.ReleaseNotes(projectId, sessionId);
                return;
            }

            // The request took a while. FinishNotes only succeeds while the conversation file still exists, so a
            // conversation (or project, or all memory) the student deleted meanwhile gets no notes, and
            // notes.json is not created again for it.
            if (!conversations.FinishNotes(projectId, sessionId, dto.Turns.Count)) return;

            IReadOnlyList<string> notes = NotesPrompt.Parse(answer);
            if (notes.Count == 0) return;
            if (!OwlyMemory.Notes.Add(projectId, sessionId, notes))
            {
                Log("the notes could not be saved.");
                return;
            }
            OwlyMemory.NotifyNotesChanged();
        }
        catch (Exception ex)
        {
            Log($"notes job failed: {ex.Message}");
            try
            {
                OwlyMemory.Conversations.ReleaseNotes(projectId, sessionId);
            }
            catch (Exception)
            {
                // Nothing more to do: the job stays unclaimed on disk only if the release itself failed.
            }
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    // The model's text, or null when anything went wrong (no CLI, not signed in, timeout, bad output).
    private static async Task<string?> AskAsync(string input)
    {
        if (!AgentRegistry.Instance.TryGet("claude", out AgentDefinition def)
            || !CliProcess.TryResolve(def.SearchPaths.GetPaths(), out string cliPath))
        {
            Log("Claude Code was not found, so no notes were written.");
            return null;
        }

        ProcessStartInfo psi = new()
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = OwlyMemory.CliDirectory,
        };
        CliProcess.ConfigureEncoding(psi);
        CliProcess.ConfigureFileName(psi, cliPath);
        AddArguments(psi);

        try
        {
            using Process proc = Process.Start(psi) ?? throw new InvalidOperationException("no process");

            // Read both pipes before writing or waiting, so a full buffer can't look like a timeout.
            Task<string> stdout = proc.StandardOutput.ReadToEndAsync();
            Task<string> stderr = proc.StandardError.ReadToEndAsync();
            try
            {
                byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);
                await proc.StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                proc.StandardInput.Close();
            }
            catch (IOException)
            {
                // The CLI quit before reading; its exit code and output say why.
            }

            bool exited;
#if NET48
            exited = await Task.Run(() => proc.WaitForExit((int)CliTimeout.TotalMilliseconds)).ConfigureAwait(false);
#else
            using (CancellationTokenSource timeout = new(CliTimeout))
            {
                try
                {
                    await proc.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                    exited = true;
                }
                catch (OperationCanceledException)
                {
                    exited = false;
                }
            }
#endif
            if (!exited)
            {
                Log("the notes request timed out.");
                if (!proc.HasExited) proc.Kill(entireProcessTree: true);
                return null;
            }

            string output = await stdout.ConfigureAwait(false);
            string errors = await stderr.ConfigureAwait(false);
            if (proc.ExitCode != 0)
            {
                Log($"the notes request exited with code {proc.ExitCode}.");
                return null;
            }
            return NotesPrompt.ReadResult(output) ?? LogNull($"the notes request gave output that could not be read ({errors.Length} characters of errors).");
        }
        catch (Exception ex)
        {
            Log($"the notes request could not run: {ex.Message}");
            return null;
        }
    }

    // The exact argument list of the notes request. Always Haiku, whatever model the student picked for chat.
    private static void AddArguments(ProcessStartInfo psi)
    {
        psi.AddArgument("-p");
        psi.AddArgument("--output-format");
        psi.AddArgument("json");
        ClaudeStreamJsonParser.AddLockdown(psi);
        psi.AddArgument("--mcp-config");
        psi.AddArgument("{\"mcpServers\":{}}");
        psi.AddArgument("--no-session-persistence");
        psi.AddArgument("--model");
        psi.AddArgument("haiku");
        psi.AddArgument("--system-prompt");
        psi.AddArgument(NotesPrompt.SystemPrompt);
    }

    private static string? LogNull(string message)
    {
        Log(message);
        return null;
    }

    private static void Log(string message) => HostUtils.LogDebugEvent($"[owly] {message}\n");
}
