# 012: A big Owly toolbar button

- **Written against:** `17f9ef6` on `rhino-9.x`. Apply it after plans 010 (art) and 011 (`OwlyWelcome` command).
- **Status:** TODO. **Part A is manual work by the team in Rhino; Part B is for the executor and is blocked until Part A's file is committed.**
- **Effort:** S (code) + M (manual)
- **Risk:** Medium. It depends on how Rhino auto-loads a plug-in's toolbar file, which differs between versions and platforms.
- **Phase:** PRD Phase 3, bullet "Implement Owly's icon" (PRD §3.3: "Accessible through an Owly owl icon in Rhino's interface")

## Why

Today a student has to know to type `Owly`. The panel tab only shows once the panel is open. The product owner wants a **big** owl button in Rhino's toolbars that opens Owly. Rhino looks for a toolbar file (`.rui`) with the **same name as the plug-in, in the same folder as the `.rhp`**, and loads it when the plug-in loads. So shipping `Owly.rui` next to `Owly.rhp` gives every student the button.

Decisions:
- One toolbar, "Owly", with one button:
  - left click `! _Owly` toggles the Owly panel
  - right click `! _OwlyWelcome` shows the welcome popup (plan 011)
  - tooltip "Owly – your Rhino tutor"
- **Big:** the button shows the owl **and** the text "Owly". It uses the **largest button size the toolbar itself allows**. Never change the student's global toolbar size (Options › Toolbars › Size & Style); that would change all of Rhino.
- It is docked at the top, after Rhino's standard toolbars, not floating in the middle of the screen.
- The button image is the team's `art/owly-small.png`, plus `art/owly-small-dark.png` if the editor has a dark-mode slot. No new artwork.

Build it by hand in Rhino's toolbar editor. A `.rui` is XML with embedded bitmaps, and hand-writing one is fragile.

## Part A: manual, by a team member in Rhino 8 for Windows

Use **Rhino 8**, so Rhino 8 and Rhino 9 can both read the file. Menu names below are from Rhino 8 for Windows. If yours differ, use the nearest equivalent and note it in the PR.

1. Build the plug-in once (`dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8`) and start Rhino 8 with the "Rhino 8" launch configuration, so `Owly` and `OwlyWelcome` exist.
2. **Tools › Toolbar Layout…** opens Options › Toolbars. Use **File › New…** and save the new file as `Owly.rui` in a temporary folder.
3. With `Owly.rui` selected in the file list, use **Toolbar › New…** to create a toolbar named `Owly`. If Rhino also asks for a group name, use `Owly` too.
4. In the new toolbar, add one button: right-click the empty toolbar and choose **New Button**, or use Rhino's documented equivalent. In the **Button Editor**:
   - **Left mouse button:** tooltip `Owly – your Rhino tutor`, command `! _Owly`
   - **Right mouse button:** tooltip `Owly welcome`, command `! _OwlyWelcome`
   - **Appearance:** choose **image and text**, with text `Owly`
   - **Image:** import `art/owly-small.png` from the repo. If the editor has a separate dark-mode image, import `art/owly-small-dark.png` there. Don't edit the image in Rhino's bitmap editor.
5. **Big:**
   - Open the toolbar's own **Properties**: right-click its title or tab, then **Properties**.
   - If it has a button-size setting, choose the largest. Don't change **Options › Toolbars › Size & Style**, which is global.
   - If there is no per-toolbar size, keep "image and text" and note it in the PR. The text label is what makes it big then.
6. **Dock it:** drag the toolbar into the top dock area, to the right of Rhino's standard toolbars.
7. **File › Save** in the Toolbars options page. Close Rhino.
8. Copy `Owly.rui` to `rhino/plugin/Owly.rui` in the repo and commit it on its own (`Add Owly toolbar (plan 012, part A)`).
9. Tell the executor that Part B can start.

## Part B: executor

### Repo conventions you must follow

- CLAUDE.md: make small, targeted changes. The plug-in's build output is set in `rhino/plugin/RhinoAI.csproj`:
  ```xml
    <PluginOutputDir Condition="'$(PluginOutputDir)' == ''">$(MSBuildProjectDirectory)/bin/$(RhinoTarget)-$(PluginOS)/$(Configuration)</PluginOutputDir>
    <OutputPath>$(PluginOutputDir)/</OutputPath>
    …
    <AppendTargetFrameworkToOutputPath Condition="'$(PluginOS)' != 'win'">false</AppendTargetFrameworkToOutputPath>
  ```
  On Windows, `Owly.rhp` lands in `…/net48/` **and** `…/net8.0/`. On Mac and Linux it lands flat in `PluginOutputDir`. Rhino looks for the `.rui` **next to the `.rhp` it loaded**, so the file has to be copied into each of those folders.
- `.github/workflows/build.yml` checks the Windows output with `test -f net48/Owly.rhp && test -f net8.0/Owly.rhp` ("Check net48 build" step).

### Scope

**Edit:**
- `rhino/plugin/RhinoAI.csproj`
- `.github/workflows/build.yml` (the one check line)
- `docs/owly/phase1-investigation.md` (one line)

**Do not touch:** `rhino/plugin/Owly.rui` (the team's file; don't edit its XML), the art, any C# code.

### Steps

**B1. Prerequisite.** `git ls-files rhino/plugin/Owly.rui` must print the path. If not, STOP: Part A isn't done. Then run the shared gates from `plans/README.md` as a baseline.

**B2. Copy the `.rui` next to every `.rhp`.** In `rhino/plugin/RhinoAI.csproj`, add this item group after the `EmbeddedResource` item group (around line 71):
```xml
  <ItemGroup>
    <!-- Owly's toolbar (plan 012). Rhino auto-loads a .rui named like the plug-in from the .rhp's own
         folder, so it is copied next to Owly.rhp for every target framework. -->
    <None Include="Owly.rui" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```
`CopyToOutputDirectory` copies into the per-framework output folder, the same folder as `Owly.rhp`. If `Owly.rui` is missing, the build fails, which is intended.

**B3. Verify the layout (Windows):**
```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary
ls rhino/plugin/bin/R8-win/Debug/net48/Owly.rui rhino/plugin/bin/R8-win/Debug/net8.0/Owly.rui rhino/plugin/bin/R9-win/Debug/net8.0/Owly.rui
```
All three files must exist, and each folder must also contain `Owly.rhp`. The R9 build may be net8.0 only; list what exists. If the `.rui` lands somewhere other than next to `Owly.rhp`, STOP and report the actual layout.

**B4. CI check.** In `.github/workflows/build.yml`, change the "Check net48 build" `run:` line from
```yaml
        run: test -f net48/Owly.rhp && test -f net8.0/Owly.rhp
```
to
```yaml
        run: test -f net48/Owly.rhp && test -f net8.0/Owly.rhp && test -f net48/Owly.rui && test -f net8.0/Owly.rui
```
The package job checks out with `lfs: true`. `Owly.rui` isn't an LFS type, so nothing else changes.

**B5. Docs.** In `docs/owly/phase1-investigation.md` line 77 ("**Toolbar highlighting (Phase 4).** Nothing in the codebase touches toolbars (`RhinoApp.ToolbarFiles` / RUI). …"), add at the end: `Plan 012 ships Owly.rui, a one-button toolbar loaded next to Owly.rhp; highlighting other toolbars' buttons is still Phase 4 work.`

**B6. Gates.** Run the shared gates.

### Done criteria

```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary   # succeeded, 0 errors
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary   # succeeded, 0 errors
dotnet test tests/Server.Tests/Server.Tests.csproj                          # all pass
test -f rhino/plugin/bin/R8-win/Debug/net48/Owly.rui && test -f rhino/plugin/bin/R8-win/Debug/net8.0/Owly.rui && echo rui-staged   # prints rui-staged
git diff --stat 17f9ef6 -- rhino/plugin/Owly.rui      # only the team's Part A commit touched it
git grep -n "Owly.rui" -- .github/workflows/build.yml   # 1 hit
```

## Manual check (by the user, after Part B)

Do this in Rhino 8, then Rhino 9.
1. **First-load behaviour.** Close Rhino. If `%APPDATA%\McNeel\Rhinoceros\<version>\UI\Plug-ins\Owly.rui` exists (Rhino's per-user copy from an earlier load), rename it. Start Rhino with the matching launch configuration.
   - Expected: an **Owly** toolbar docked at the top with the owl and "Owly", at the large size.
2. Left-click the button: the Owly panel opens. Click again: it closes. Right-click: the welcome popup opens.
3. Hover: the tooltip reads "Owly – your Rhino tutor".
4. Dark theme: the owl is visible. If no dark image was imported, note how it looks.
5. Record in the PR **where Rhino keeps its copy** of `Owly.rui` and **whether a newer `Owly.rui` next to the `.rhp` replaces that copy** on the next start. Test this by changing the tooltip in the repo file, rebuilding and restarting. The answer goes into this plan's Maintenance section.
6. Mac (if available): report whether Rhino 8 for Mac shows the toolbar. Mac isn't blocking.

## Tests

None automated. The CI check (B4) guards the packaging.

## Maintenance

- **Changing the owl art doesn't update the toolbar by itself.** The `.rui` embeds its own copy of the image. After replacing `art/owly-small*.png` (plan 010), redo Part A step 4's image import, save, and commit the new `Owly.rui`.
- Rhino keeps a per-user copy of a plug-in's toolbar after the first load. Students who already have that copy may need to reset it (Part A step 1 of the manual check) to see a changed toolbar. Fill in the exact behaviour from manual check 5.
- Phase 4's "tool highlighting" may need to find buttons in other `.rui` files. This plan doesn't touch `RhinoApp.ToolbarFiles`.

## STOP and report if

- Part A isn't committed (B1).
- The `.rui` doesn't end up next to `Owly.rhp` (B3).
- Manual check 1: Rhino 8 or 9 doesn't show the toolbar on first load. A fallback is to open it from code with `RhinoApp.ToolbarFiles.Open(path)` on first idle, but that is a behaviour change that needs the product owner's approval first.
