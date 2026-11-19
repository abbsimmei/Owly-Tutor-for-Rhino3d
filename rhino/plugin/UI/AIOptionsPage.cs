using System.Drawing;

using Rhino.UI;
using Rhino.Runtime;

namespace Rhino.AI;

internal sealed class AIOptionsPage : OptionsDialogPage
{
    private AISettingsPanel Panel { get; } = new();
    private Image? LightCachedImage { get; set; }
    private Image? DarkCachedImage { get; set; }

    public AIOptionsPage() : base(Localization.LocalizeString("Owly", 2))
    {
        Panel.Width = 800;
        Panel.UseRhinoStyle();
    }

    public override object PageControl => Panel;

    // Mac's Settings UI lists pages by icon; a page with no PageImage never shows in the navigation.
    public override Image PageImage => HostUtils.RunningInDarkMode switch
    {
        true => DarkCachedImage ??= OwlyArt.DrawingBitmap(OwlyArtSize.Small, dark: true),
        _ => LightCachedImage ??= OwlyArt.DrawingBitmap(OwlyArtSize.Small, dark: false),
    };

    public override bool OnApply() => Panel.TryCommit(out _);

    public override bool OnActivate(bool active)
    {
        if (active)
            Modified = true;
        return base.OnActivate(active);
    }
}
