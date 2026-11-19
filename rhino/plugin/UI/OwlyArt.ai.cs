using System.IO;
using System.Reflection;
using Rhino.Runtime;

namespace Rhino.AI;

internal enum OwlyArtSize { Small, Large }

// Owly's owl, embedded from art/owly*.png (plan 010). Every path here keeps the PNG's alpha channel,
// so replacing the art with transparent PNGs needs no code change: no JPEG/BMP/24-bit conversions,
// and never drawn onto a filled background.
internal static class OwlyArt
{
    public static bool DarkTheme => HostUtils.RunningInDarkMode;

    public static string ResourceName(OwlyArtSize size, bool dark) =>
        "Rhino.AI.owly"
        + (size == OwlyArtSize.Small ? "-small" : string.Empty)
        + (dark ? "-dark" : string.Empty)
        + ".png";

    // Fully buffered, so callers can dispose it even when an image type keeps reading its source.
    public static MemoryStream Open(OwlyArtSize size, bool dark)
    {
        string name = ResourceName(size, dark);
        using Stream source = typeof(OwlyArt).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded art '{name}' is missing.");
        MemoryStream copy = new();
        source.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    // Eto image for dialogs and pages. pixels scales it square with high-quality interpolation.
    public static Eto.Drawing.Bitmap EtoBitmap(OwlyArtSize size, int? pixels = null, bool? dark = null)
    {
        using MemoryStream stream = Open(size, dark ?? DarkTheme);
        Eto.Drawing.Bitmap original = new(stream);
        if (pixels is not int px)
            return original;

        Eto.Drawing.Bitmap scaled = new(original, px, px, Eto.Drawing.ImageInterpolation.High);
        original.Dispose();
        return scaled;
    }

    // Title-bar icon for Owly's own windows.
    public static Eto.Drawing.Icon WindowIcon() => new(1f, EtoBitmap(OwlyArtSize.Small));

    // A 32bpp ARGB System.Drawing copy, detached from its stream, for RhinoCommon APIs that take
    // System.Drawing images (OptionsDialogPage.PageImage).
    public static System.Drawing.Bitmap DrawingBitmap(OwlyArtSize size, bool? dark = null)
    {
        using MemoryStream stream = Open(size, dark ?? DarkTheme);
        using System.Drawing.Bitmap original = new(stream);
        return new System.Drawing.Bitmap(original);
    }
}
