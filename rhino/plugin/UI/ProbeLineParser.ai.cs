using System.Text.RegularExpressions;

namespace Rhino.AI.UI;

// Plan 024: reads the options off Rhino's first prompt line, e.g. "Center of base ( NumSides=8  Mode=Inscribed  Edge )".
// Rhino-free so Server.Tests can link it. Never throws; anything unexpected gives null.
internal sealed record ProbeOption(string Name, string? Value);

internal static class ProbeLineParser
{
    private static readonly Regex Shape = new(@"^(?<prompt>.*?)\s\(\s+(?<opts>.*?)\s+\)\s*\z", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex Gap = new(@"\s{2,}", RegexOptions.CultureInvariant);
    private static readonly Regex NameRx = new(@"^[A-Za-z][A-Za-z0-9]*\z", RegexOptions.CultureInvariant);
    private static readonly Regex ValueRx = new(@"^[A-Za-z0-9.\-]+\z", RegexOptions.CultureInvariant);
    private const int MaxLine = 2000;

    public static IReadOnlyList<ProbeOption>? Parse(string? line)
    {
        try
        {
            if (line is null) return null;
            line = line.Trim();
            if (line.Length == 0 || line.Length > MaxLine) return null;
            if (line.EndsWith(":", StringComparison.Ordinal)) line = line.Substring(0, line.Length - 1).TrimEnd();

            var m = Shape.Match(line);
            if (!m.Success) return null;

            var result = new List<ProbeOption>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in Gap.Split(m.Groups["opts"].Value))
            {
                var token = raw.Trim();
                if (token.Length == 0) continue;
                string name = token;
                string? value = null;
                var eq = token.IndexOf('=');
                if (eq >= 0)
                {
                    name = token.Substring(0, eq);
                    value = token.Substring(eq + 1).Trim();
                    if (!ValueRx.IsMatch(value)) continue;
                }
                if (!NameRx.IsMatch(name)) continue;
                if (!seen.Add(name)) continue;
                result.Add(new ProbeOption(name, value));
            }
            return result.Count == 0 ? null : result;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
