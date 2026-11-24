using System.Collections.ObjectModel;
using Eto.Drawing;
using Eto.Forms;
using Rhino.AI.UI;
using Rhino.UI;

namespace Rhino.AI;

// Shared settings UI: in Owly, just the model choice (plan 008). Hosted by both the standalone
// AISettingsDialog and the Rhino Options page (AIOptionsPage); neither owns the commit logic,
// it lives here behind TryCommit so the two hosts can never drift.
internal sealed class AISettingsPanel : Panel
{
    private ObservableCollection<AgentRow> Rows { get; } = [];

    // Right-hand property list for the selected agent. Model/Enabled/Available used to live in the grid;
    // they moved here so the grid stays a plain selection list and every editable property reads top-down.
    private Label NameHeader { get; } = new() { Font = Eto.Drawing.Fonts.Sans(13, FontStyle.Bold) };
    private Label AvailableLabel { get; } = new();
    private DropDown ModelBox { get; } = new();
    private TextArea SearchPathsBox { get; } = new() { Wrap = false, Height = 70, ReadOnly = true };

    private CheckBox AutoLoadMcpBox { get; } = new() { Text = Localization.LocalizeString("Load MCP server at startup", 14) };

    private bool LoadingTabShown { get; set; }

    // Sentinel shown in the grid's Model dropdown for an empty model. Picking it stores an empty
    // string, i.e. "pass no --model, let the CLI choose its own default".
    private static string DefaultModelLabel => Localization.LocalizeString("(default)", 16);

    // Suppresses the editor->row write-back while we are programmatically loading
    // the editor from a freshly selected row.
    private bool Loading { get; set; }

    private List<(string Name, AIPage Page)> Pages { get; } = [];

    protected override void OnLoad(EventArgs e)
    {
        if (Content is TabControl tabs)
        {
            tabs.Pages.Clear();
#if R9
            if (AIInstances.Preferences.UseSdk)
            {
                // Human written pages
                AddPage(tabs, Localization.LocalizeString("AI Agents", 17), new AIAgentDefinitionsPage());
                tabs.Pages.Add(new TabPage { Text = Localization.LocalizeString("Loading", 20), Content = LoadingTab() }); // TODO : Use UserPreferences
                AddPage(tabs, LOC.STR("Usage"), new AIUsagePage());
                AddPage(tabs, LOC.STR("PlugIns"), new AIPluginPage());
                AddPage(tabs, LOC.STR("Permissions"), new AIPermissionsPage());
            }
            else
#endif
            {
                tabs.Pages.Add(new TabPage { Text = Localization.LocalizeString("Owly", 2), Content = AgentsTab() });
            }
        }

        base.OnLoad(e);
    }

    protected override void OnUnLoad(EventArgs e)
    {
        base.OnUnLoad(e);
    }

    public AISettingsPanel()
    {
        Padding = new Padding(20);
        Height = 600;

        SeedRows();

        TabControl tabs = new();
        Content = tabs;
    }

    private void AddPage(TabControl tabs, string name, AIPage page)
    {
        tabs.Pages.Add(new TabPage { Text = name, Content = page });
        Pages.Add((name, page));
    }

    // Persists every tab back to AISettings. Returns false when a page is invalid, so the
    // hosting dialog/page can keep itself open; true once everything is saved.
    public bool TryCommit(out string error)
    {
        error = string.Empty;

        foreach ((string name, AIPage page) in Pages)
        {
            if (page.Validate()) continue;
            error = string.Format(Localization.LocalizeString("The {0} settings are not valid.", 255), name);
            return false;
        }

        foreach ((string name, AIPage page) in Pages)
        {
            if (page.TryApplyChanges()) continue;
            error = string.Format(Localization.LocalizeString("The {0} settings could not be applied.", 256), name);
            return false;
        }

        foreach (AgentRow row in Rows)
            AISettings.SetAgentModel(row.Name, row.Model);

        if (LoadingTabShown)
            AISettings.AutoLoadMCP = AutoLoadMcpBox.Checked == true;

        return true;
    }

    private void SeedRows()
    {
        foreach (AgentDefinition def in AgentRegistry.Instance.AllDefinitions)
            Rows.Add(AgentRow.From(def));
    }

    private Control AgentsTab()
    {
        ModelBox.SelectedValueChanged += (_, _) => WriteEditor(row => row.Model = CurrentModelValue());

        TableLayout properties = new()
        {
            Spacing = new Size(8, 6),
            Rows =
            {
                new TableRow(NameHeader),
                new TableRow(AvailableLabel),
                new TableRow(LabeledColumn(Localization.LocalizeString("Model:", 23), ModelBox)),
                new TableRow(DeleteMemoryRow()),
                new TableRow(LabeledColumn(Localization.LocalizeString("Found at:", 24), SearchPathsBox)),
                new TableRow { ScaleHeight = true },
            },
        };

        LoadEditor();
        return new TableLayout
        {
            Padding = new Padding(8),
            Rows = { new TableRow(properties) { ScaleHeight = true } },
        };
    }

    // Label stacked above its control, so a multi-line box gets full width instead of a left-hand label
    // eating into it.
    private static Control LabeledColumn(string label, Control control) =>
        new TableLayout
        {
            Spacing = new Size(0, 3),
            Rows = { new TableRow(new Label { Text = label }), new TableRow(new TableCell(control, true)) },
        };

    // Plan 025 / ADR-0009: everything Owly keeps in %AppData%\Owly, gone in one step. It happens at
    // once, not on OK, because the confirmation is the commit.
    private Control DeleteMemoryRow()
    {
        Button button = new() { Text = Localization.LocalizeString("Delete all Owly memory", 363) };
        button.Click += (_, _) => DeleteAllMemory();
        return new TableLayout { Rows = { new TableRow(button, null) } };
    }

    private void DeleteAllMemory()
    {
        DialogResult answer = MessageBox.Show(
            this,
            Localization.LocalizeString("Delete all Owly memory? Owly forgets your projects, which files belong to them, and everything it saved about them. This can't be undone.", 364),
            Localization.LocalizeString("Delete all Owly memory", 365),
            MessageBoxButtons.YesNo,
            MessageBoxType.Warning,
            MessageBoxDefaultButton.No);
        if (answer != DialogResult.Yes) return;

        if (OwlyMemory.DeleteAll())
            MessageBox.Show(this, Localization.LocalizeString("Owly's memory on this computer is deleted.", 366), MessageBoxType.Information);
        else
            MessageBox.Show(this, Localization.LocalizeString("Owly couldn't delete everything. Close the other Rhino if it is open, then try again.", 367), MessageBoxType.Error);
    }

    private static string FoundString { get; } = Localization.LocalizeString("Found on search paths", 217);
    private static string NotFoundString { get; } = Localization.LocalizeString("Not found on search paths", 218);

    private void LoadEditor()
    {
        Loading = true;
        try
        {
            if (TryGetSelected(out AgentRow row))
            {
                NameHeader.Text = row.Name;
                AvailableLabel.Text = row.Available ? $"✓ {FoundString}" : $"✗ {NotFoundString}";
                AvailableLabel.TextColor = row.Available ? Colors.Green : Colors.Red;
                LoadModelBox(row);
                SearchPathsBox.Text = row.SearchPathsText;
                EnableEditor(true);
            }
            else
            {
                NameHeader.Text = Localization.LocalizeString("No agent selected", 30);
                AvailableLabel.Text = string.Empty;
                ModelBox.Items.Clear();
                SearchPathsBox.Text = string.Empty;
                EnableEditor(false);
            }
        }
        finally { Loading = false; }
    }

    // Fills the editor's Model dropdown for the selected row: the "(default)" sentinel first, then the
    // adapter's choices. The box stays editable so a model not in the list can be typed and is remembered
    // on save. Only ever called under the Loading guard, so the resulting TextChanged does not write back.
    private void LoadModelBox(AgentRow row)
    {
        ModelBox.Items.Clear();
        ModelBox.Items.Add(new ListItem
        {
            Text = row.DefaultModel.Length > 0 ? $"{DefaultModelLabel} - {row.Models.FirstOrDefault(spec => spec.Id == row.DefaultModel)?.Label() ?? row.DefaultModel}" : DefaultModelLabel,
            Key = string.Empty,
        });

        foreach (ModelSpec spec in row.Models)
            ModelBox.Items.Add(new ListItem { Text = spec.Label(), Key = spec.Id });

        ModelBox.SelectedKey = row.Model;
    }

    // The "(default)" sentinel carries an empty key, so picking it falls back to the definition's own model.
    private string CurrentModelValue() => ModelBox.SelectedKey ?? string.Empty;

    private void EnableEditor(bool enabled)
    {
        ModelBox.Enabled = enabled;
    }

    private void WriteEditor(Action<AgentRow> apply)
    {
        if (Loading)
            return;
        if (TryGetSelected(out AgentRow row))
            apply(row);
    }

    // Owly has exactly one agent (ADR-0003), so the editor always shows it.
    private bool TryGetSelected(out AgentRow row)
    {
        row = Rows.FirstOrDefault()!;
        return row is not null;
    }

    private Control LoadingTab()
    {
        LoadingTabShown = true;
        AutoLoadMcpBox.Checked = AISettings.AutoLoadMCP;

        Label help = new()
        {
            Wrap = WrapMode.Word,
            Text = Localization.LocalizeString("What Owly starts when Rhino launches. Changes take effect the next time Rhino starts.", 36),
            TextColor = Colors.Gray,
        };

        return new TableLayout
        {
            Padding = new Padding(8),
            Spacing = new Size(0, 8),
            Rows =
            {
                new TableRow(help),
                new TableRow(AutoLoadMcpBox),
                new TableRow { ScaleHeight = true },
            },
        };
    }

    // Only Model is user-editable; the rest mirrors the definition.
    private sealed class AgentRow
    {
        public string Name { get; }
        public bool Available { get; }
        public string SearchPathsText { get; }
        public IReadOnlyList<ModelSpec> Models { get; }
        public string DefaultModel { get; }

        public string Model { get; set; }

        public AgentRow(
            string name, bool available, string searchPathsText, IReadOnlyList<ModelSpec> models,
            string defaultModel, string model)
        {
            Name = name;
            Available = available;
            SearchPathsText = searchPathsText;
            Models = models;
            DefaultModel = defaultModel;
            Model = model;
        }

        public static AgentRow From(AgentDefinition def) =>
            new(
                def.Name,
                def.Available,
                string.Join(Environment.NewLine, def.SearchPaths.GetPaths()),
                def.Models,
                def.DefaultModel,
                AISettings.AgentModel(def.Name));
    }
}
