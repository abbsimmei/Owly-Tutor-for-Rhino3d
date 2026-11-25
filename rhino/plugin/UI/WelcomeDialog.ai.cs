using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;

namespace Rhino.AI;

// Owly's welcome popup (plan 011): shown once after Rhino's first start with Owly installed, and on
// demand from the OwlyWelcome command and "About Owly" in Owly Settings. Returns true when the
// student chose "Open Owly".
internal sealed class WelcomeDialog : Dialog<bool>
{
    private const int TextWidth = 420;

    public WelcomeDialog()
    {
        Title = Localization.LocalizeString("Welcome to Owly", 267);
        Padding = new Padding(16);
        Resizable = false;
        Icon = OwlyArt.WindowIcon();
        this.UseRhinoStyle();

        ImageView logo = new() { Image = OwlyArt.EtoBitmap(OwlyArtSize.Large, 128), Size = new Size(128, 128) };
        Label name = new() { Text = Localization.LocalizeString("Owly", 2), Font = SystemFonts.Bold(16), TextAlignment = TextAlignment.Center };
        Label slogan = new() { Text = Localization.LocalizeString("Owly help, Only Help!", 268), TextAlignment = TextAlignment.Center };

        Label does = Paragraph(Localization.LocalizeString("Owly is your Rhino tutor. Describe what you want to make, and Owly recommends the Rhino commands and tools that fit and explains how to use them.", 269));
        Label wont = Paragraph(Localization.LocalizeString("Owly never models for you. You always do the modelling yourself.", 270));
        Label privacy = Paragraph(Localization.LocalizeString("Your questions, and what Owly reads from your model (such as selected objects and layers), are sent to Anthropic through Claude Code. Owly also saves your conversations and short notes about your projects on this computer until you delete them, and includes them when it asks Claude. Claude Code keeps its own copy on this computer for about 30 days. If you turn on main memory in settings, Owly also keeps a short list of what you find hard, on this computer, and includes it when it asks Claude. Owly also looks up command help on docs.mcneel.com, which sends the command's name.", 437));
        Label needs = Paragraph(Localization.LocalizeString("Owly needs Claude Code, installed and signed in on this computer.", 272));

        LinkButton setup = new() { Text = Localization.LocalizeString("How to install Claude Code", 273) };
        setup.Click += (_, _) => Application.Instance.Open(DocsLinks.ClaudeCodeSetup);

        Button openButton = new() { Text = Localization.LocalizeString("Open Owly", 274) };
        openButton.Click += (_, _) => Close(true);

        Button closeButton = new() { Text = Localization.LocalizeString("Close", 275) };
        closeButton.Click += (_, _) => Close(false);

        StackLayout buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            Items = { null, closeButton, openButton },
        };

        Content = new StackLayout
        {
            Spacing = 10,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Items =
            {
                new StackLayoutItem(logo, HorizontalAlignment.Center),
                new StackLayoutItem(name, HorizontalAlignment.Center),
                new StackLayoutItem(slogan, HorizontalAlignment.Center),
                does,
                wont,
                privacy,
                needs,
                setup,
                buttons,
            },
        };

        DefaultButton = openButton;
        AbortButton = closeButton;
    }

    // Shows the popup over owner, and opens the Owly panel when the student picks "Open Owly".
    public static void ShowFor(Window? owner)
    {
        bool open = owner is null ? new WelcomeDialog().ShowModal() : new WelcomeDialog().ShowModal(owner);
        if (open)
            Panels.OpenPanel(UI.AIPanel.PanelId);
    }

    private static Label Paragraph(string text) => new() { Text = text, Wrap = WrapMode.Word, Width = TextWidth };
}
