using System.Text;

namespace Rhino.AI;

// Plan 026 / ADR-0009: when Claude Code no longer has the session file of a saved conversation (it
// keeps them about 30 days), the saved transcript is handed back as text at the start of the first
// message. Only the student's questions and Owly's replies; tool calls and results are left out. It
// goes over stdin with that message and never onto the command line, which cmd.exe limits to 8,191
// characters. Rhino-free, so Server.Tests links it.
internal static class ResumeContext
{
    public const int DefaultCap = 4000;

    private const string Header = "Earlier in this conversation (for context only):\n";
    private const string Footer = "End of the earlier conversation. The student's new message follows.";

    // The newest whole turns that fit in cap characters, framing included. When even the newest turn
    // is too long, its tail is kept. "" when there are no turns, or no room.
    public static string Build(IReadOnlyList<TurnDto> turns, int cap = DefaultCap)
    {
        if (turns.Count == 0) return string.Empty;

        int room = cap - Header.Length - Footer.Length;
        if (room <= 0) return string.Empty;

        List<string> kept = [];
        for (int i = turns.Count - 1; i >= 0; i--)
        {
            string text = Render(turns[i]);
            if (text.Length <= room)
            {
                kept.Insert(0, text);
                room -= text.Length;
                continue;
            }

            if (kept.Count == 0)
                kept.Add(Tail(text, room));
            break;
        }

        if (kept.Count == 0) return string.Empty;
        StringBuilder sb = new(Header);
        foreach (string text in kept)
            sb.Append(text);
        sb.Append(Footer);
        return sb.ToString();
    }

    private static string Render(TurnDto turn)
    {
        StringBuilder reply = new();
        foreach (TurnEventDto ev in turn.Events)
            if (ev.Kind == TurnEventKind.AssistantText)
                reply.Append(ev.Text);

        string answer = reply.ToString().Trim();
        string text = $"Student: {turn.Prompt.Trim()}\n";
        return answer.Length == 0 ? text : text + $"Owly: {answer}\n";
    }

    // The last count characters, never starting inside a surrogate pair.
    private static string Tail(string text, int count)
    {
        int start = text.Length - count;
        if (start > 0 && char.IsLowSurrogate(text[start])) start++;
        return text.Substring(start);
    }
}
