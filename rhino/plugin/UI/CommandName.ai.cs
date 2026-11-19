using System.Text.RegularExpressions;

namespace Rhino.AI.UI;

// The shape of a Rhino command as Owly writes it (plan 014): `_Loft`, `_-Loft`. The panel only makes chips
// from this shape (\z, not $, because .NET lets $ match before a trailing newline); the host checks it again so nothing else can reach the clipboard through command.copy.
internal static class CommandName
{
    private static readonly Regex Shape = new("^_-?[A-Za-z][A-Za-z0-9_]*\\z", RegexOptions.CultureInvariant);

    public static bool IsValid(string? text) => text is not null && Shape.IsMatch(text);
}
