using System.Net;
using System.Text.RegularExpressions;

namespace Rhino.AI.UI;

// Plan 023: the options of a command, read from the command's help page. Rhino-free so Server.Tests can link it.
// Kind: "choice" | "toggle" | "number" | "info". Run (RunMacro) writes choice, toggle and number; "info" is read-only.
// Current and FirstPrompt come only from the option probe (plan 024, OptionMerge).
internal sealed record CommandOptionValue(string Name, string Description);
internal sealed record CommandOption(string Name, string Kind, IReadOnlyList<CommandOptionValue> Values, string Description, string? Current = null, bool? FirstPrompt = null);
internal sealed record CommandOptions(IReadOnlyList<CommandOption> Options)
{
    public static readonly CommandOptions Empty = new(Array.Empty<CommandOption>());
}

// A small tolerant tag scanner. The pages are HTML, not XML (&#160;, <br /> inside <p>, unclosed nesting), so no
// XDocument. It never throws: anything unexpected gives CommandOptions.Empty.
internal static class CommandOptionsParser
{
    private const int MaxOptions = 30;
    private const int MaxDescription = 600;
    private static readonly Regex Ident = new("^[A-Za-z][A-Za-z0-9]*\\z", RegexOptions.CultureInvariant);
    private static readonly Regex TagRx = new("<(/?)([A-Za-z][A-Za-z0-9]*)\\b([^>]*)>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ScriptRx = new("<(script|style)\\b[^>]*>.*?</\\1\\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CommentRx = new("<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex AnyTagRx = new("<[^>]*>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex SpacesRx = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex ClassRx = new("\\bclass\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IdRx = new("\\bid\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RoleRx = new("\\brole\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed class Tag
    {
        public int Start, End;      // offsets in the source
        public bool Closing;
        public string Name = "";
        public string Attrs = "";

        public bool HasClass(string c)
        {
            var m = ClassRx.Match(Attrs);
            return m.Success && m.Groups[1].Value.Split(' ').Contains(c);
        }
    }

    private static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase) { "br", "img", "hr", "input", "meta", "link" };

    public static CommandOptions Parse(string? html)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(html)) return CommandOptions.Empty;
            return ParseCore(html!);
        }
        catch (Exception)
        {
            return CommandOptions.Empty;
        }
    }

    private static CommandOptions ParseCore(string html)
    {
        html = CommentRx.Replace(ScriptRx.Replace(html, " "), " ");

        // Narrow to the main content when it is there.
        var mainStart = 0;
        foreach (Match m in TagRx.Matches(html))
        {
            if (m.Groups[1].Length == 0 && m.Groups[2].Value.Equals("div", StringComparison.OrdinalIgnoreCase))
            {
                var id = IdRx.Match(m.Groups[3].Value);
                if (id.Success && id.Groups[1].Value == "mc-main-content")
                {
                    mainStart = m.Index + m.Length;
                    break;
                }
            }
        }
        var tags = new List<Tag>();
        foreach (Match m in TagRx.Matches(html))
        {
            if (m.Index < mainStart) continue;
            tags.Add(new Tag { Start = m.Index, End = m.Index + m.Length, Closing = m.Groups[1].Length > 0, Name = m.Groups[2].Value.ToLowerInvariant(), Attrs = m.Groups[3].Value });
        }

        // A page can document several commands; a later <h1> starts another one. Only the first counts.
        var seenH1 = false;
        for (var i = 0; i < tags.Count; i++)
        {
            if (tags[i].Closing || tags[i].Name != "h1") continue;
            if (!seenH1) seenH1 = true;
            else { tags.RemoveRange(i, tags.Count - i); break; }
        }

        var options = new List<CommandOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string name, List<CommandOptionValue> values, string description)
        {
            if (options.Count >= MaxOptions || !Ident.IsMatch(name)) return;
            // Yes/No/On/Off are values (sometimes drawn in layout tables), never options.
            if (name.Equals("yes", StringComparison.OrdinalIgnoreCase) || name.Equals("no", StringComparison.OrdinalIgnoreCase)
                || name.Equals("on", StringComparison.OrdinalIgnoreCase) || name.Equals("off", StringComparison.OrdinalIgnoreCase)) return;
            if (!seen.Add(name)) return;
            var vals = values.GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            options.Add(new CommandOption(name, KindOf(vals), vals, description));
        }

        // Layout B: a <table> whose header says "Command-line options".
        for (var i = 0; i < tags.Count; i++)
        {
            var t = tags[i];
            if (t.Closing || t.Name != "th") continue;
            var close = FindClose(tags, i);
            if (close < 0) continue;
            if (Clean(html, t.End, tags[close].Start) != "Command-line options") continue;
            var table = EnclosingTable(tags, i);
            if (table < 0) continue;
            var tableEnd = FindClose(tags, table);
            if (tableEnd < 0) tableEnd = tags.Count - 1;
            ParseTable(html, tags, table, tableEnd, Add);
        }

        // Layout A: <p class="Dialog_Box_Title">Command-line options</p>, then siblings.
        for (var i = 0; i < tags.Count; i++)
        {
            var t = tags[i];
            if (t.Closing || !t.HasClass("Dialog_Box_Title")) continue;
            var close = FindClose(tags, i);
            if (close < 0) continue;
            if (Clean(html, t.End, tags[close].Start) != "Command-line options") continue;
            ParseSection(html, tags, close + 1, Add);
        }

        return options.Count == 0 ? CommandOptions.Empty : new CommandOptions(options);
    }

    private static string KindOf(IReadOnlyList<CommandOptionValue> values)
    {
        if (values.Count == 2)
        {
            var a = values[0].Name.ToLowerInvariant();
            var b = values[1].Name.ToLowerInvariant();
            if ((a == "yes" && b == "no") || (a == "no" && b == "yes") || (a == "on" && b == "off") || (a == "off" && b == "on")) return "toggle";
        }
        return values.Count >= 2 ? "choice" : "info";
    }

    // Index of the closing tag matching tags[open], counting same-named nesting. -1 when it never closes.
    private static int FindClose(List<Tag> tags, int open)
    {
        var name = tags[open].Name;
        if (Void.Contains(name)) return open;
        var depth = 0;
        for (var j = open; j < tags.Count; j++)
        {
            if (tags[j].Name != name) continue;
            depth += tags[j].Closing ? -1 : 1;
            if (depth == 0) return j;
        }
        return -1;
    }

    private static int EnclosingTable(List<Tag> tags, int from)
    {
        var depth = 0;
        for (var j = from; j >= 0; j--)
        {
            if (tags[j].Name != "table") continue;
            if (tags[j].Closing) depth++;
            else if (depth == 0) return j;
            else depth--;
        }
        return -1;
    }

    private static string Clean(string html, int from, int to)
    {
        if (to <= from) return "";
        var s = AnyTagRx.Replace(html.Substring(from, to - from), " ");
        s = WebUtility.HtmlDecode(s).Replace('\u00a0', ' ');
        return SpacesRx.Replace(s, " ").Trim();
    }

    private static string Cap(string s) => s.Length <= MaxDescription ? s : s.Substring(0, MaxDescription).TrimEnd();

    // The text of the next <p> after a heading, stopping at the next heading or div.
    private static string DescriptionAfter(string html, List<Tag> tags, int afterIdx)
    {
        for (var j = afterIdx; j < tags.Count; j++)
        {
            var t = tags[j];
            if (t.Closing) { if (t.Name == "div") return ""; continue; }
            if (t.Name is "h1" or "h2" or "h3" or "h4" or "h5" or "div") return "";
            if (t.Name == "p")
            {
                var c = FindClose(tags, j);
                var end = c < 0 ? Math.Min(html.Length, t.End + 4000) : tags[c].Start;
                return Cap(Clean(html, t.End, end));
            }
        }
        return "";
    }

    private static string HeadingText(string html, List<Tag> tags, int idx, out int close)
    {
        close = FindClose(tags, idx);
        if (close < 0) return "";
        return Clean(html, tags[idx].End, tags[close].Start);
    }

    private static void ParseSection(string html, List<Tag> tags, int start, Action<string, List<CommandOptionValue>, string> add)
    {
        // Walk the tags in order. Wrapper divs are transparent, except Indent_Level_2, which holds values.
        List<CommandOptionValue>? curValues = null;
        string? curName = null;
        var curDesc = "";
        var skipping = false;

        void Flush()
        {
            if (curName != null) add(curName, curValues ?? new List<CommandOptionValue>(), curDesc);
            curName = null; curValues = null; curDesc = "";
        }

        var i = start;
        while (i < tags.Count)
        {
            var t = tags[i];
            if (t.Closing) { i++; continue; }

            if (t.HasClass("Dialog_Box_Title") || t.Name == "h2") break;
            if (t.Name == "div")
            {
                var r = RoleRx.Match(t.Attrs);
                if (r.Success && r.Groups[1].Value == "main") break;
            }

            if (t.Name == "h4") { Flush(); skipping = true; i++; continue; }

            if (t.Name == "div" && t.HasClass("Indent_Level_2"))
            {
                var c = FindClose(tags, i);
                var end = c < 0 ? tags.Count : c;
                // A nested Indent_Level_2 means the block holds sub-options, not values: the option stays info and
                // nothing in the block becomes an option.
                var nested = false;
                for (var k = i + 1; k < end && k < tags.Count; k++)
                    if (!tags[k].Closing && tags[k].Name == "div" && tags[k].HasClass("Indent_Level_2")) { nested = true; break; }
                if (nested) curValues?.Clear();
                else if (curName != null && !skipping) CollectValues(html, tags, i + 1, end, curValues!);
                i = end + 1;
                continue;
            }

            if (t.Name == "h5")
            {
                var name = HeadingText(html, tags, i, out var close);
                if (close < 0) { i++; continue; }
                Flush();
                skipping = false;
                if (Ident.IsMatch(name))
                {
                    curName = name;
                    curValues = new List<CommandOptionValue>();
                    curDesc = DescriptionAfter(html, tags, close + 1);
                }
                i = close + 1;
                continue;
            }
            i++;
        }
        Flush();
    }

    private static void CollectValues(string html, List<Tag> tags, int from, int to, List<CommandOptionValue> values)
    {
        for (var j = from; j < to && j < tags.Count; j++)
        {
            if (tags[j].Closing || tags[j].Name != "h5") continue;
            var name = HeadingText(html, tags, j, out var close);
            if (close < 0) continue;
            if (Ident.IsMatch(name)) values.Add(new CommandOptionValue(name, DescriptionAfter(html, tags, close + 1)));
            j = close;
        }
    }

    private static void ParseTable(string html, List<Tag> tags, int table, int tableEnd, Action<string, List<CommandOptionValue>, string> add)
    {
        for (var i = table; i < tableEnd; i++)
        {
            if (tags[i].Closing || tags[i].Name != "tr") continue;
            var rowEnd = FindClose(tags, i);
            if (rowEnd < 0 || rowEnd > tableEnd) rowEnd = tableEnd;
            var cells = new List<(int open, int close)>();
            for (var j = i + 1; j < rowEnd; j++)
            {
                if (tags[j].Closing || tags[j].Name != "td") continue;
                var c = FindClose(tags, j);
                if (c < 0 || c > rowEnd) c = rowEnd;
                cells.Add((j, c));
                j = c;
            }
            i = rowEnd;
            if (cells.Count < 2) continue;
            var name = Clean(html, tags[cells[0].open].End, tags[cells[0].close].Start);
            if (!Ident.IsMatch(name)) continue;

            var (o, c2) = cells[1];
            var values = new List<CommandOptionValue>();
            // Values only count before the first tutorial (an <h4> or a tutorialblock div); the description stops at
            // the first of those or the first <h5>.
            var tutorial = -1;
            var firstHeading = -1;
            for (var j = o + 1; j < c2; j++)
            {
                var tj = tags[j];
                if (tj.Closing) continue;
                if (tutorial < 0 && (tj.Name == "h4" || (tj.Name == "div" && tj.HasClass("tutorialblock")))) tutorial = j;
                if (firstHeading < 0 && tj.Name == "h5") firstHeading = j;
            }
            // Mode shape: the first <h5> comes before any tutorial, so every <h5> outside a tutorialblock is a value.
            // Tangent shape: a tutorial comes first, so there are no values. An <h5> inside a tutorialblock is never one.
            if (firstHeading >= 0 && (tutorial < 0 || firstHeading < tutorial))
            {
                for (var j = o + 1; j < c2; j++)
                {
                    var tj = tags[j];
                    if (tj.Closing) continue;
                    if (tj.Name == "div" && tj.HasClass("tutorialblock"))
                    {
                        var tc = FindClose(tags, j);
                        j = tc < 0 ? c2 : tc;
                        continue;
                    }
                    if (tj.Name != "h5") continue;
                    var vn = HeadingText(html, tags, j, out var vclose);
                    if (vclose < 0) continue;
                    if (Ident.IsMatch(vn)) values.Add(new CommandOptionValue(vn, DescriptionAfter(html, tags, vclose + 1)));
                    j = vclose;
                }
            }
            var stop = c2;
            if (firstHeading >= 0 && firstHeading < stop) stop = firstHeading;
            if (tutorial >= 0 && tutorial < stop) stop = tutorial;
            var descEnd = tags[stop].Start;
            add(name, values, Cap(Clean(html, tags[o].End, descEnd)));
        }
    }
}
