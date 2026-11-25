using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;

namespace Rhino.AI;

internal sealed class AISettingsDialog : Dialog
{
    private AISettingsPanel Panel { get; } = new();

    public AISettingsDialog()
    {
        Title = Localization.LocalizeString("Owly Settings", 10);
        Padding = new Padding(12);
        Size = new Size(720, 680);
        MinimumSize = new Size(560, 440);
        Resizable = true;
        Icon = OwlyArt.WindowIcon();

        this.UseRhinoStyle();

        Button saveButton = new() { Text = Localization.LocalizeString("Save", 11) };
        saveButton.Click += (_, _) =>
        {
            if (Panel.TryCommit(out _))
            {
                Close();
            }
        };

        Button closeButton = new() { Text = Localization.LocalizeString("Cancel", 12) };
        closeButton.Click += (_, _) => Close();

        Button aboutButton = new() { Text = Localization.LocalizeString("About Owly", 276) };
        aboutButton.Click += (_, _) =>
        {
            // The popup saves its own choices, so re-read them or a later Save would undo them.
            if (WelcomeDialog.ShowFor(this))
                Panel.ReloadLevelAndMemory();
        };

        StackLayout buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            Items = { aboutButton, null, closeButton, saveButton },
        };

        Content = new TableLayout
        {
            Spacing = new Size(0, 8),
            Rows =
            {
                new TableRow(Panel) { ScaleHeight = true },
                new TableRow(buttons),
            },
        };

        DefaultButton = saveButton;
        AbortButton = closeButton;
    }
}
