# 002: Identity and verification baseline

- **Written against:** `ba4b4a0`
- **Status:** TODO
- **Effort:** S
- **Risk:** Low

## Why

Owly is a fork of McNeel's RhinoAI, which Rhino 9 ships built in. Commit `b5e89d3` gave Owly its own plugin GUID and the assembly name `Owly`, but other identifiers still collide with the built-in plugin. Rhino resolves panels by GUID and commands by name, so these collisions stop Owly and RhinoAI from working side by side. CI is also broken: it checks for `RhinoAI.rhp`, which the build no longer produces. And CI still has a job that publishes to McNeel's package feed. See `docs/adr/0004-coexist-with-builtin-rhinoai.md`.

## Scope (files you may change)

- `rhino/plugin/UI/AIPanel.cs`
- `rhino/plugin/Commands/AIPanelCommand.cs`
- `rhino/plugin/Commands/TestAgenticCommand.cs`
- `rhino/plugin/Agents/Commands/ClaudeCommand.cs`
- `rhino/plugin/Agents/Commands/CodexCommand.cs`
- `rhino/plugin/Agents/Commands/AgentCommand.cs`
- `rhino/plugin/RhinoAIPlugin.cs`
- `rhino/plugin/UI/AIOptionsPage.cs`
- `rhino/plugin/manifest.yml`
- `rhino/plugin/RhinoAI.csproj` (only the `PropertyGroup` values listed below)
- `.github/workflows/build.yml`
- `.vscode/launch.json`
- `.vscode/tasks.json`

## Out of scope (do not touch)

- The namespace `Rhino.AI`, the class names, the `RhinoAI.csproj` file name and the `[RhinoAI]` log strings.
- The router, the MCP commands and the tools. Later plans remove them.
- The plugin GUID in `Properties/AssemblyInfo.cs` / `AISettings.cs`. It is already unique.

## Steps

1. **New panel GUID.** In `rhino/plugin/UI/AIPanel.cs`, lines 7-8 currently read:
   ```csharp
   // Inherited from the Eto panel this replaced, so saved Rhino layouts still resolve the AI panel.
   [Guid("fb948c98-5987-45a3-8dcb-2814ed77ee3b")]
   ```
   Generate a fresh GUID (`[guid]::NewGuid()` in PowerShell) and replace the line. Change the comment to `// Owly's own panel id; must differ from the built-in RhinoAI panel (fb948c98-...).`
   Verify: `git grep -n "fb948c98-5987-45a3-8dcb-2814ed77ee3b" -- rhino` matches only that comment line.

2. **Rename the panel commands.** In `rhino/plugin/Commands/AIPanelCommand.cs`:
   - `LOC.COMMANDNAME("AI")` becomes `LOC.COMMANDNAME("Owly")`.
   - `LOC.COMMANDNAME("AIPanel")` becomes `LOC.COMMANDNAME("OwlyPanel")`.
   - In the alias, `RhinoApp.RunScript(doc.RuntimeSerialNumber, "AI", false)` becomes `"Owly"`.

3. **Remove the other command-name collisions.**
   - `TestAgenticCommand.cs`: change `EnglishName => "TestAgentic"` to `"OwlyTestAgentic"`.
   - Delete `rhino/plugin/Agents/Commands/ClaudeCommand.cs`, `CodexCommand.cs` and `AgentCommand.cs`. They are hidden commands named `Claude` and `Codex` that send command-line text to an agent; Owly's only entry point is the panel. Before deleting, run `git grep -n "AgentCommand\b" -- rhino` and confirm nothing else derives from or references `AgentCommand`. If something does, STOP and report.

4. **Panel title and options page.**
   - In `rhino/plugin/RhinoAIPlugin.cs:24`, change `Localization.LocalizeString("AI", 1)` to `Localization.LocalizeString("Owly", 1)`.
   - In `rhino/plugin/UI/AIOptionsPage.cs:19`, change `Localization.LocalizeString("AI", 2)` to `Localization.LocalizeString("Owly", 2)`.

5. **Fix the icon bug.** `rhino/plugin/RhinoAIPlugin.cs` lines 12-13 both name the dark icon:
   ```csharp
   private const string IconResourceName = "Rhino.AI.Panel_dark.ico";
   private const string DarkIconResourceName = "Rhino.AI.Panel_dark.ico";
   ```
   Change the first one to `"Rhino.AI.Panel_light.ico"`. `art/Panel_light.ico` exists and is embedded as `Rhino.AI.Panel_light.ico`.

6. **Package identity.**
   - `rhino/plugin/manifest.yml`: set `name: Owly`. Set `authors:` to a single entry, `- Owly team (KTH)`. Set `description: Owly - an AI tutor that teaches architecture students how to use Rhino. It never models for you.` Delete the `url:` line.
   - `rhino/plugin/RhinoAI.csproj`: set `<PackageId>Owly.PlugIn</PackageId>`, `<Title>Owly</Title>` and `<Description>Owly - AI tutor for Rhino</Description>`. In the line
     `<PluginOutputDir Condition="... '$(RhinoPlugInOutputDir)' != ''">$([MSBuild]::NormalizePath('$(RhinoPlugInOutputDir)', 'RhinoAI'))</PluginOutputDir>`
     change `'RhinoAI'` to `'Owly'`.

7. **CI.** In `.github/workflows/build.yml`:
   - In the "Check net48 build" step, change `test -f net48/RhinoAI.rhp && test -f net8.0/RhinoAI.rhp` to use `Owly.rhp`.
   - Delete the whole `release:` job, the last job in the file. It pushes to McNeel's yak feed with `YAK_TOKEN`.
   - Change `permissions: contents: write` to `contents: read`.
   - Rename the artifact `rhino-mcp-platform-...` to `owly-...`.

8. **Dev tooling.**
   - `.vscode/launch.json`: in the "Rhino 9 BETA" config, rename it to "Rhino 9" and change the Windows program to `C:\\Program Files\\Rhino 9\\System\\Rhino.exe`.
   - `.vscode/tasks.json`: in both `uninstall yak` tasks, change `"Rhino-MCP-Platform"` to `"Owly"`.
   - Leave the router and docs launch entries; plan 003 removes them.

## Done criteria

- The shared gates in `plans/README.md` pass (R9 build, R8 build, Server.Tests).
- `git grep -nE 'COMMANDNAME\("AI"\)|"TestAgentic"|Rhino-MCP-Platform|RhinoAI\.rhp' -- rhino .github .vscode` prints nothing.
- `ls rhino/plugin/bin/R9-win/Debug/net8.0/Owly.rhp` exists on Windows.
- Manual check by the user (the executor cannot do this): install the build in Rhino 9 next to the built-in RhinoAI. Both panels appear, and the `Owly` command toggles Owly's panel.

## Tests

No new unit tests; these are identifier changes. The shared gates are the regression check.

## Maintenance

- If a later plan adds commands, prefix their names with `Owly` to avoid collisions.
- Saved Rhino window layouts that referenced the old panel GUID will no longer restore the panel. That is expected.

## STOP and report if

- Any build error mentions `AgentCommand`, `ClaudeCommand` or `CodexCommand` after step 3, other than the deleted files themselves.
- `Panel_light.ico` is not an embedded resource (check `<EmbeddedResource Include="../../art/*.svg;../../art/*.ico" />` in the csproj).
