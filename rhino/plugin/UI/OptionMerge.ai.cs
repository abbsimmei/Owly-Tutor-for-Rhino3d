using System.Text.RegularExpressions;

namespace Rhino.AI.UI;

// Plan 024: merges the options Rhino shows at the first prompt (the probe) with the help page's data. Rhino-free.
// The probe gives names, kinds and current values; the help page gives choices and descriptions. Only probed options
// can be written by Run; options only on the help page become read-only "info" rows.
internal static class OptionMerge
{
    private static readonly Regex NumberRx = new("^-?[0-9]+(\\.[0-9]+)?\\z", RegexOptions.CultureInvariant);
    private static readonly CommandOptionValue[] YesNo = { new("Yes", ""), new("No", "") };
    private static readonly CommandOptionValue[] OnOff = { new("On", ""), new("Off", "") };
    private static readonly CommandOptionValue[] NoValues = Array.Empty<CommandOptionValue>();

    public static CommandOptions Merge(CommandOptions help, IReadOnlyList<ProbeOption>? probe)
    {
        if (probe is null || probe.Count == 0) return help;

        var merged = new List<CommandOption>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in probe)
        {
            if (!used.Add(p.Name)) continue;
            var h = help.Options.FirstOrDefault(o => string.Equals(o.Name, p.Name, StringComparison.OrdinalIgnoreCase));
            var desc = h?.Description ?? "";
            var hv = h?.Values ?? NoValues;

            if (p.Value is null)
            {
                merged.Add(new CommandOption(p.Name, "info", hv, desc, null, true));
            }
            else if (NumberRx.IsMatch(p.Value))
            {
                merged.Add(new CommandOption(p.Name, "number", NoValues, desc, p.Value, true));
            }
            else if (IsPair(p.Value, "yes", "no") || IsPair(p.Value, "on", "off"))
            {
                var yes = IsPair(p.Value, "yes", "no");
                var canon = yes
                    ? (p.Value.Equals("yes", StringComparison.OrdinalIgnoreCase) ? "Yes" : "No")
                    : (p.Value.Equals("on", StringComparison.OrdinalIgnoreCase) ? "On" : "Off");
                var values = h is { Kind: "toggle" } ? h.Values : (yes ? YesNo : OnOff);
                merged.Add(new CommandOption(p.Name, "toggle", values, desc, canon, true));
            }
            else
            {
                var match = h is { Kind: "choice" }
                    ? h.Values.FirstOrDefault(v => string.Equals(v.Name, p.Value, StringComparison.OrdinalIgnoreCase))
                    : null;
                if (h is not null && match is not null)
                    merged.Add(new CommandOption(p.Name, "choice", h.Values, desc, match.Name, true));
                else
                    merged.Add(new CommandOption(p.Name, "info", hv, desc, p.Value, true));
            }
        }

        foreach (var h in help.Options)
            if (!used.Contains(h.Name))
                merged.Add(new CommandOption(h.Name, "info", h.Values, h.Description, null, null));

        return new CommandOptions(merged);
    }

    private static bool IsPair(string value, string a, string b) =>
        value.Equals(a, StringComparison.OrdinalIgnoreCase) || value.Equals(b, StringComparison.OrdinalIgnoreCase);
}
