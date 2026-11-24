using System.Text;
using System.Text.RegularExpressions;

namespace Rhino.AI.UI;

// Plan 023: builds the macro Run sends, from the host's OWN cached option data (never the panel's lists).
// Only choice/toggle options and values that are listed there are written, with the data's spelling.
// Every token contains '=', which is never a command name, so no token can start a second command.
internal static class RunMacro
{
    private static readonly Regex Ident = new("^[A-Za-z][A-Za-z0-9]*\\z", RegexOptions.CultureInvariant);

    public static string Build(string name, IReadOnlyDictionary<string, string>? changes, CommandOptions data)
    {
        if (!CommandName.IsValid(name)) throw new ArgumentException("Not a command name.", nameof(name));
        if (changes is null || changes.Count == 0) return name;

        var sb = new StringBuilder(name);
        foreach (var opt in data.Options)
        {
            if (opt.Kind != "choice" && opt.Kind != "toggle") continue;
            if (!Ident.IsMatch(opt.Name)) continue;
            string? wanted = null;
            foreach (var kv in changes)
                if (string.Equals(kv.Key, opt.Name, StringComparison.OrdinalIgnoreCase)) { wanted = kv.Value; break; }
            if (wanted is null || !Ident.IsMatch(wanted)) continue;
            var value = opt.Values.FirstOrDefault(v => string.Equals(v.Name, wanted, StringComparison.OrdinalIgnoreCase));
            if (value is null || !Ident.IsMatch(value.Name)) continue;
            sb.Append(" _").Append(opt.Name).Append("=_").Append(value.Name);
        }
        return sb.ToString();
    }
}
