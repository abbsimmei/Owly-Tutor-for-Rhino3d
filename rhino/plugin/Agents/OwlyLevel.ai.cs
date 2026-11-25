using System;

namespace Rhino.AI;

// How well the student knows Rhino (plan 029). Global, chosen in the welcome popup, changed in settings.
internal enum OwlyLevel
{
    Beginner,
    Intermediate,
    Advanced,
}

internal static class OwlyLevelExtensions
{
    // Case-insensitive; anything unknown, empty or null is Beginner.
    public static OwlyLevel Parse(string? text) =>
        Enum.TryParse(text?.Trim(), ignoreCase: true, out OwlyLevel level) && Enum.IsDefined(typeof(OwlyLevel), level) ? level : OwlyLevel.Beginner;
}
