# 011: Welcome popup

- **Written against:** `17f9ef6` on `rhino-9.x`. Apply it after plans 009 (adds `RhinoAIPlugin.OnFirstIdle`) and 010 (adds `OwlyArt`).
- **Status:** TODO
- **Effort:** M
- **Risk:** Low–Medium. It adds a new modal Eto dialog and a first-run flag. The only risk is the timing of a dialog at Rhino start-up.
- **Phase:** PRD Phase 3, bullet "Add the welcome popup"

## Why

PRD §3.3: "On first installation, display a welcome popup with the Owly logo and a short description." PRD §7 also asks that users are informed about data processing, and nothing in Owly says so yet. The product owner decided:

- **When:** once, right after Rhino finishes starting for the first time with Owly installed. A plug-in update doesn't show it again. The flag is stored in Owly's plug-in settings, which Rhino keeps per Rhino version, so Rhino 8 and Rhino 9 each show it once. That's acceptable.
- **Buttons:** **Open Owly** (default; opens the Owly panel) and **Close**.
- **Reopen:** a new `OwlyWelcome` command, and an **About Owly** button in the Owly Settings dialog.
- **Content** (English; the team may edit the wording later):
  - the owl (`art/owly.png` / `owly-dark.png` via plan 010's `OwlyArt`), the name "Owly", and the slogan "Owly help, Only Help!"
  - what Owly does: "Owly is your Rhino tutor. Describe what you want to make, and Owly recommends the Rhino commands and tools that fit and explains how to use them."
  - what it won't do: "Owly never models for you. You always do the modelling yourself."
  - **privacy**, this exact text, agreed with the product owner and matching ADR-0006: "Your questions, and what Owly reads from your model (such as selected objects and layers), are sent to Anthropic through Claude Code. Owly doesn't save your conversations. Claude Code keeps its own copy on this computer for about 30 days."
  - requirement: "Owly needs Claude Code, installed and signed in on this computer.", plus a link "How to install Claude Code" to `https://code.claude.com/docs/en/setup`. That is the same URL the panel's empty state uses (`rhino/panel/src/ui/empty.ts:61`).

## Repo conventions you must follow

- New C# files written by an AI agent are named `*.ai.cs`.
- New commands get an `Owly` prefix (CLAUDE.md), so they can't clash with the RhinoAI plug-in built into Rhino 9. Rhino finds public `Command` subclasses by reflection, so there's nothing to register. Follow `rhino/plugin/Commands/AIPanelCommand.cs`:
  ```csharp
  using RhinoCommand = Rhino.Commands.Command;
  using Rhino.UI;

  namespace Rhino.AI;

  public class AIPanelCommand : RhinoCommand
  {
      public override string EnglishName => LOC.COMMANDNAME("Owly");

      protected override string CommandContextHelpUrl => DocsLinks.Homepage;

      protected override Commands.Result RunCommand(RhinoDoc doc, Commands.RunMode mode)
      { … }
  }
  ```
- **Localization IDs.** Each user string is `Localization.LocalizeString("text", <id>)`. A new string gets the next unused ID; plan 010 used 266. Find the current maximum with:
  ```sh
  git grep -hoE "LocalizeString\(\"[^\"]*\", [0-9]+\)" -- rhino/plugin | grep -oE "[0-9]+\)$" | tr -d ')' | sort -n | tail -1
  ```
  Then number this plan's strings consecutively upward from there, in the order listed in step 3.
- **Eto dialogs** follow `rhino/plugin/UI/AISettingsDialog.cs`: `Padding = new Padding(12)`, `this.UseRhinoStyle();`, a right-aligned horizontal `StackLayout` of buttons with a `null` spacer, and `DefaultButton` / `AbortButton`.
- **Settings booleans** follow `AISettings.AutoLoadMCP` (`rhino/plugin/AISettings.cs:31-35`):
  ```csharp
    public static bool AutoLoadMCP
    {
        get => Settings.GetBool(nameof(AutoLoadMCP), false);
        set => Settings.SetBool(nameof(AutoLoadMCP), value);
    }
  ```
- `System` comes in through `rhino/plugin/GlobalUsings.cs`; other namespaces need explicit `using`s.

## Scope

**Add:**
- `rhino/plugin/UI/WelcomeDialog.ai.cs`
- `rhino/plugin/Commands/WelcomeCommand.ai.cs`

**Edit:**
- `rhino/plugin/AISettings.cs`: add `WelcomeShown`.
- `rhino/plugin/RhinoAIPlugin.cs`: extend `OnFirstIdle` (from plan 009).
- `rhino/plugin/UI/AISettingsDialog.cs`: add the About Owly button.
- `rhino/plugin/DocsLinks.cs`: add `ClaudeCodeSetup`.
- `docs/owly/phase1-investigation.md`

**Out of scope. Do not touch:**
- The web panel (`rhino/panel/**`). The popup is a native Eto dialog.
- `AISettingsPanel.ai.cs`. The About button goes in the dialog's button row only, so it does not appear inside Rhino Options.
- `DocsLinks.Homepage` / `GettingStarted` (old McNeel URLs). That's a separate clean-up.
- The art files.

## Steps

### 1. Baseline

Run the shared gates from `plans/README.md`. Confirm that the prerequisites exist:
```sh
git grep -n "OnFirstIdle" -- rhino/plugin/RhinoAIPlugin.cs      # plan 009
git grep -n "class OwlyArt" -- rhino/plugin                      # plan 010
```
If either is missing, STOP: plans 009 and 010 have to land first.

### 2. Settings flag and link

**`rhino/plugin/AISettings.cs`**: add this after `AutoLoadMCP`:
```csharp
    // True once the welcome popup has been shown on this computer (plan 011). PersistentSettings are
    // kept per Rhino version, so Rhino 8 and Rhino 9 each show it once.
    public static bool WelcomeShown
    {
        get => Settings.GetBool(nameof(WelcomeShown), false);
        set => Settings.SetBool(nameof(WelcomeShown), value);
    }
```

**`rhino/plugin/DocsLinks.cs`**: add this after `Homepage`:
```csharp
    // Installing and signing in to Claude Code, which Owly needs. Same URL as the panel's empty state (empty.ts).
    public const string ClaudeCodeSetup = "https://code.claude.com/docs/en/setup";
```

### 3. `rhino/plugin/UI/WelcomeDialog.ai.cs` (new)

Write the dialog below. Replace each `<id N>` with consecutive IDs counting up from the maximum the grep found, in the order shown (the title gets the first one). "Owly" itself reuses existing ID 2, the same as `AIOptionsPage.cs:19`.

```csharp
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
        Title = Localization.LocalizeString("Welcome to Owly", <id 1>);
        Padding = new Padding(16);
        Resizable = false;
        Icon = OwlyArt.WindowIcon();
        this.UseRhinoStyle();

        ImageView logo = new() { Image = OwlyArt.EtoBitmap(OwlyArtSize.Large, 128), Size = new Size(128, 128) };
        Label name = new() { Text = Localization.LocalizeString("Owly", 2), Font = SystemFonts.Bold(16), TextAlignment = TextAlignment.Center };
        Label slogan = new() { Text = Localization.LocalizeString("Owly help, Only Help!", <id 2>), TextAlignment = TextAlignment.Center };

        Label does = Paragraph(Localization.LocalizeString("Owly is your Rhino tutor. Describe what you want to make, and Owly recommends the Rhino commands and tools that fit and explains how to use them.", <id 3>));
        Label wont = Paragraph(Localization.LocalizeString("Owly never models for you. You always do the modelling yourself.", <id 4>));
        Label privacy = Paragraph(Localization.LocalizeString("Your questions, and what Owly reads from your model (such as selected objects and layers), are sent to Anthropic through Claude Code. Owly doesn't save your conversations. Claude Code keeps its own copy on this computer for about 30 days.", <id 5>));
        Label needs = Paragraph(Localization.LocalizeString("Owly needs Claude Code, installed and signed in on this computer.", <id 6>));

        LinkButton setup = new() { Text = Localization.LocalizeString("How to install Claude Code", <id 7>) };
        setup.Click += (_, _) => Application.Instance.Open(DocsLinks.ClaudeCodeSetup);

        Button openButton = new() { Text = Localization.LocalizeString("Open Owly", <id 8>) };
        openButton.Click += (_, _) => Close(true);

        Button closeButton = new() { Text = Localization.LocalizeString("Close", <id 9>) };
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
```
Notes:
- `Panels` is `Rhino.UI.Panels` and `UI.AIPanel.PanelId` is what `AIPanelCommand` uses. `OpenPanel` on an already open panel is harmless.
- If `SystemFonts.Bold(16)`, `TextAlignment`, `WrapMode.Word` or `StackLayoutItem(control, HorizontalAlignment)` don't compile against this Eto version, use the nearest Eto equivalent and say so in your report. Don't drop any of the content.
- The owl must keep its alpha. Use `OwlyArt.EtoBitmap` only, and set no background colour on `logo`.

### 4. First run (`rhino/plugin/RhinoAIPlugin.cs`)

Plan 009 added:
```csharp
    // One-shot work that needs the plugin fully loaded (AISettings resolves it with PlugIn.Find).
    private void OnFirstIdle(object? sender, EventArgs e)
    {
        RhinoApp.Idle -= OnFirstIdle;
        try
        {
            AISettings.DeleteLegacyConversations();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not clear old conversations: {ex.Message}");
        }
    }
```
Add the welcome step at the end of the method, after the existing `try/catch`:
```csharp
        // Welcome popup, once per computer and Rhino version (plan 011). The flag is set before
        // showing, so a crash inside the dialog can never make it pop up on every start.
        if (RhinoApp.IsRunningHeadless || AISettings.WelcomeShown)
            return;

        AISettings.WelcomeShown = true;
        try
        {
            WelcomeDialog.ShowFor(RhinoEtoApp.MainWindow);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[owly] could not show the welcome popup: {ex.Message}");
        }
```
Add `using Rhino.UI;` if it isn't already there; `RhinoEtoApp` lives in `Rhino.UI`. If reading `AISettings.WelcomeShown` throws, the plug-in isn't findable yet. Catch that and log it, and don't set the flag.

### 5. `rhino/plugin/Commands/WelcomeCommand.ai.cs` (new)

```csharp
using RhinoCommand = Rhino.Commands.Command;
using Rhino.UI;

namespace Rhino.AI;

// Shows Owly's welcome popup again (plan 011). Doesn't change the first-run flag.
public class WelcomeCommand : RhinoCommand
{
    public override string EnglishName => LOC.COMMANDNAME("OwlyWelcome");

    protected override string CommandContextHelpUrl => DocsLinks.Homepage;

    protected override Commands.Result RunCommand(RhinoDoc doc, Commands.RunMode mode)
    {
        WelcomeDialog.ShowFor(RhinoEtoApp.MainWindow);
        return Commands.Result.Success;
    }
}
```

### 6. About Owly in the settings dialog (`rhino/plugin/UI/AISettingsDialog.cs`)

After `closeButton` is created (line 31), add:
```csharp
        Button aboutButton = new() { Text = Localization.LocalizeString("About Owly", <id 10>) };
        aboutButton.Click += (_, _) => WelcomeDialog.ShowFor(this);
```
and change the button row's items (line 38) from `Items = { null, closeButton, saveButton },` to:
```csharp
            Items = { aboutButton, null, closeButton, saveButton },
```
That puts About on the left and Cancel/Save on the right.

### 7. Docs (`docs/owly/phase1-investigation.md`)

- Line 67, the table row `` | Eto dialog pattern (`MCPConnectCommand.cs` ConnectDialog) | **Reuse as a template** | Welcome popup | ``: change the Verdict and Why cells to `**Done**` and `` ConnectDialog was deleted in plan 003; the welcome popup (plan 011) follows `AISettingsDialog` ``.
- Line 79, the bullet "**Welcome popup (Phase 3).** There is no first-run flag yet. …": add at its end: `Done in plan 011: AISettings.WelcomeShown, WelcomeDialog, the OwlyWelcome command.`

### 8. Build

Run the shared gates.

## Done criteria

```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary   # succeeded, 0 errors
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary   # succeeded, 0 errors
dotnet test tests/Server.Tests/Server.Tests.csproj                          # all pass
git grep -n "OwlyWelcome" -- rhino/plugin/Commands                          # 1 hit
git grep -n "WelcomeShown" -- rhino/plugin                                  # AISettings.cs + RhinoAIPlugin.cs
git grep -n "about 30 days" -- rhino/plugin/UI/WelcomeDialog.ai.cs          # 1 hit (privacy line present)
git grep -n "code.claude.com/docs/en/setup" -- rhino/plugin/DocsLinks.cs    # 1 hit
git grep -hoE "LocalizeString\(\"[^\"]*\", [0-9]+\)" -- rhino/plugin | grep -oE ", [0-9]+\)$" | sort | uniq -d   # prints only ", 2)" (ID 2 = "Owly" is shared on purpose)
```

## Manual check (by the user, Rhino 9, then Rhino 8)

1. **Reset the flag.** Quit Rhino. Under `%APPDATA%\McNeel\Rhinoceros\<version>\Plug-ins\`, open the folder whose name contains `1f0762a7-df2f-4d11-8ff3-22ec1a40391c`, then its `settings` file(s), and delete the `WelcomeShown` entry if present. On a machine that never ran this build there is nothing to delete.
2. Start Rhino. Once it has finished starting, the "Welcome to Owly" popup appears: the owl, "Owly", the slogan, the two description lines, the privacy line, the Claude Code line and link, then Close and Open Owly.
3. Press **Open Owly**. The popup closes and the Owly panel opens.
4. Quit and restart Rhino. No popup.
5. Run `OwlyWelcome`. The popup appears. Press **Close**: nothing else happens.
6. Panel gear → Owly Settings → **About Owly**. The popup appears over the settings dialog.
7. Click "How to install Claude Code". The browser opens the setup page.
8. Switch Rhino to dark theme, restart, and run `OwlyWelcome`. The dark owl is shown and the text is readable.

## Tests

No automated tests. The dialog and the first-run timing are Rhino UI and aren't linked into Server.Tests.

## Maintenance

- If the privacy facts change (for example Phase 5 adds `--no-session-persistence`, see ADR-0006), update the privacy string here, keeping its ID, together with ADR-0006.
- The popup's text lives only in C#. The web panel doesn't show it.

## STOP and report if

- Plans 009 or 010 haven't landed (step 1).
- The popup appears before Rhino's main window is ready, or blocks Rhino's start-up in a way that needs a different trigger than the first `Idle`.
- `RhinoEtoApp.MainWindow` isn't available on Rhino 8. Report it; `ShowFor(null)` is the fallback.
