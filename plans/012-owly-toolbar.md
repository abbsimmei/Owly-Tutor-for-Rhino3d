# 012: A big Owly toolbar button

- **Written against:** `6476e45` on `rhino-9.x` (plans 009-011 applied). Revised from the first version, which made building the toolbar a manual job in Rhino's editor. The product owner chose to have it hand-written instead.
- **Status:** TODO
- **Effort:** S
- **Risk:** Medium. Rhino has to accept a hand-written toolbar file and draw a PNG wrapped in SVG. Both are checked by hand in Rhino at the end, with fallbacks.
- **Phase:** PRD Phase 3, bullet "Implement Owly's icon" (PRD §3.3: "Accessible through an Owly owl icon in Rhino's interface")

## Why

Students can't find Owly without typing `Owly`: Owly ships no toolbar button. Rhino loads a toolbar file (`.rui`) that has **the same name as the plug-in and sits in the same folder as its `.rhp`**. Rhino's own Script Editor builds plug-ins exactly this way. For example, a plug-in built on the product owner's machine has `SimonMeierPlugIn.rhp` and `SimonMeierPlugIn.rui` side by side in its build folder. So shipping `Owly.rui` next to `Owly.rhp` gives every student an Owly button.

**Not part of this plan:** Rhino 9's standard toolbar has a question-mark button between "Launch Grasshopper" and "Launch Grasshopper 2". That button belongs to McNeel's built-in RhinoAI. Its macro is `! _AI` ("Toggle RhinoAI panel"), and it does nothing when that plug-in isn't loaded. Owly must not edit or hide Rhino's own toolbars, and it must not register an `AI` command (ADR-0004).

Decisions by the product owner:
- One toolbar, "Owly", with one button:
  - left click `! _Owly` toggles the Owly panel
  - right click `! _OwlyWelcome` shows the welcome popup (plan 011)
- **Big:** the button shows the owl **and** the text "Owly". Never change the student's global toolbar size.
- Docked at the top, not floating.
- The image is the team's `art/owly-small.png` (light theme) and `art/owly-small-dark.png` (dark theme), embedded **byte for byte**. Never create, redraw, trace or re-encode the art.

## Format

The format comes from a real Rhino-generated plug-in toolbar file (Rhino 9 Script Editor output, `RhinoUI major_ver="5"`). The structure, cut down:
```xml
<RhinoUI guid="…" major_ver="5" minor_ver="0" localize="False" default_language_id="1033">
  <extend_rhino_menus />
  <menus />
  <tool_bar_groups>
    <tool_bar_group guid="…">
      <text><locale_1033>Name</locale_1033></text>
      <dock_bar_info visible="True" floating="True" />
      <tool_bar_group_item guid="…" major_version="1" minor_version="1">
        <text><locale_1033>Name</locale_1033></text>
        <tool_bar_id>…toolbar guid…</tool_bar_id>
      </tool_bar_group_item>
    </tool_bar_group>
  </tool_bar_groups>
  <tool_bars>
    <tool_bar guid="…" bitmap_id="…" item_display_style="control_only">
      <text><locale_1033>Name</locale_1033></text>
      <tool_bar_item guid="…" button_display_mode="control_only" button_style="normal">
        <text><locale_1033>Button</locale_1033></text>
        <left_macro_id>…macro guid…</left_macro_id>
      </tool_bar_item>
    </tool_bar>
  </tool_bars>
  <macros>
    <macro_item guid="…" bitmap_id="…icon guid…">
      <text><locale_1033>Button</locale_1033></text>
      <tooltip><locale_1033></locale_1033></tooltip>
      <script>! _Command</script>
    </macro_item>
  </macros>
  <icons>
    <icon guid="…icon guid…">
      <light><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">…</svg></light>
      <dark><svg …>…</svg></dark>
    </icon>
  </icons>
  <bitmaps />
</RhinoUI>
```
Rhino's own `default.rui` uses `<right_macro_id>` for right-click macros, next to `<left_macro_id>`. A docked bar there looks like `<dock_bar_info … docking="True" visible="True" floating="False" dock_location="top" … />`.

The icon format only takes SVG. So the owl goes in as an SVG whose only content is an `<image>` holding the PNG as a `data:image/png;base64,…` URI. The PNG bytes are untouched, so a future transparent PNG keeps its alpha.

## Repo conventions you must follow

- The plug-in build output is set in `rhino/plugin/RhinoAI.csproj`:
  ```xml
    <OutputPath>$(PluginOutputDir)/</OutputPath>
    …
    <AppendTargetFrameworkToOutputPath Condition="'$(PluginOS)' != 'win'">false</AppendTargetFrameworkToOutputPath>
  ```
  On Windows, `Owly.rhp` lands in `…/net48/` **and** `…/net8.0/`; on Mac it lands flat. The `.rui` has to be copied next to every `Owly.rhp`.
- `.github/workflows/build.yml`'s "Check net48 build" step runs `test -f net48/Owly.rhp && test -f net8.0/Owly.rhp`.
- Don't edit the art, any C# code, `plans/`, or anything outside Scope.

## Scope

**Add:** `rhino/plugin/Owly.rui`

**Edit:** `rhino/plugin/RhinoAI.csproj`, `.github/workflows/build.yml` (one line), `docs/owly/phase1-investigation.md` (one line)

**Out of scope:** Rhino's own `default.rui` in the user's profile, the `art/` files, all C# and panel code.

## Steps

### 1. Baseline

Run the shared gates from `plans/README.md` (R9 build, R8 build, Server.Tests). If they fail, STOP. Check that the commands the button calls exist:
```sh
git grep -n 'COMMANDNAME("Owly")\|COMMANDNAME("OwlyWelcome")' -- rhino/plugin/Commands   # 2 hits
```

### 2. Write the template `rhino/plugin/Owly.rui`

Create the file with exactly this content. The two `{{…}}` placeholders are filled in step 3. The GUIDs are fixed; don't regenerate them, so future edits keep the same toolbar identity.
```xml
<RhinoUI guid="035224d9-ce66-482d-a4b1-a1f9032f2887" major_ver="5" minor_ver="0" localize="False" default_language_id="1033">
  <extend_rhino_menus />
  <menus />
  <tool_bar_groups>
    <tool_bar_group guid="17aac2ea-7e48-495b-9364-808b95d90eb5">
      <text><locale_1033>Owly</locale_1033></text>
      <dock_bar_info dock_bar="False" docking="True" horz="False" visible="True" floating="False" dock_location="top" />
      <tool_bar_group_item guid="0f422f56-74f4-4b80-87a2-c69827695dd3" major_version="1" minor_version="1">
        <text><locale_1033>Owly</locale_1033></text>
        <tool_bar_id>771e320b-3dc3-4d38-b6ba-661f2e77d2ab</tool_bar_id>
      </tool_bar_group_item>
    </tool_bar_group>
  </tool_bar_groups>
  <tool_bars>
    <tool_bar guid="771e320b-3dc3-4d38-b6ba-661f2e77d2ab" bitmap_id="fbff7d0b-529d-4fae-856c-6ff9bb592de5" item_display_style="image_and_text">
      <text><locale_1033>Owly</locale_1033></text>
      <tool_bar_item guid="5e8f9308-b33a-4545-b773-76186de4552e" button_style="normal">
        <text><locale_1033>Owly</locale_1033></text>
        <left_macro_id>71e6904b-d5ee-48b9-87ab-035ae64dae27</left_macro_id>
        <right_macro_id>ec2df67f-8df6-4228-9c02-0067cd249252</right_macro_id>
      </tool_bar_item>
    </tool_bar>
  </tool_bars>
  <macros>
    <macro_item guid="71e6904b-d5ee-48b9-87ab-035ae64dae27" bitmap_id="4ecc1dce-50fb-444b-8715-fc14c6ca58ba">
      <text><locale_1033>Owly</locale_1033></text>
      <tooltip><locale_1033>Owly – your Rhino tutor</locale_1033></tooltip>
      <help_text><locale_1033>Open or close the Owly panel</locale_1033></help_text>
      <button_text><locale_1033>Owly</locale_1033></button_text>
      <script>! _Owly</script>
    </macro_item>
    <macro_item guid="ec2df67f-8df6-4228-9c02-0067cd249252" bitmap_id="4ecc1dce-50fb-444b-8715-fc14c6ca58ba">
      <text><locale_1033>Owly welcome</locale_1033></text>
      <tooltip><locale_1033>Owly welcome</locale_1033></tooltip>
      <script>! _OwlyWelcome</script>
    </macro_item>
  </macros>
  <icons>
    <icon guid="4ecc1dce-50fb-444b-8715-fc14c6ca58ba">
      <light><svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 64 64" width="64" height="64"><image x="0" y="0" width="64" height="64" href="data:image/png;base64,{{LIGHT_PNG_BASE64}}" xlink:href="data:image/png;base64,{{LIGHT_PNG_BASE64}}" /></svg></light>
      <dark><svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 64 64" width="64" height="64"><image x="0" y="0" width="64" height="64" href="data:image/png;base64,{{DARK_PNG_BASE64}}" xlink:href="data:image/png;base64,{{DARK_PNG_BASE64}}" /></svg></dark>
    </icon>
  </icons>
  <bitmaps />
</RhinoUI>
```

### 3. Embed the art (Windows PowerShell, from the repo root)

```powershell
$rui   = Resolve-Path "rhino/plugin/Owly.rui"
$light = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path "art/owly-small.png")))
$dark  = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path "art/owly-small-dark.png")))
$text  = [IO.File]::ReadAllText($rui).Replace('{{LIGHT_PNG_BASE64}}', $light).Replace('{{DARK_PNG_BASE64}}', $dark)
[IO.File]::WriteAllText($rui, $text, (New-Object Text.UTF8Encoding $false))   # UTF-8 without BOM
```
Then verify:
```powershell
[xml](Get-Content -Raw rhino/plugin/Owly.rui) | Out-Null; "well-formed"           # prints well-formed
Select-String -Path rhino/plugin/Owly.rui -Pattern '\{\{' -Quiet                  # False (no placeholder left)
# The embedded bytes equal the art files exactly:
$x = [xml](Get-Content -Raw rhino/plugin/Owly.rui)
$h = $x.RhinoUI.icons.icon.light.svg.image.href -replace '^data:image/png;base64,',''
[Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path "art/owly-small.png"))) -eq $h   # True
```
Check `git diff --stat -- art/`: it must be empty.

### 4. Copy it next to every `Owly.rhp`

In `rhino/plugin/RhinoAI.csproj`, add after the `EmbeddedResource` item group:
```xml
  <ItemGroup>
    <!-- Owly's toolbar (plan 012). Rhino loads a .rui named like the plug-in from the .rhp's own folder,
         so it is copied next to Owly.rhp for every target framework. -->
    <None Include="Owly.rui" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```
Build and check:
```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary
ls rhino/plugin/bin/R9-win/Debug/*/Owly.rhp rhino/plugin/bin/R9-win/Debug/*/Owly.rui rhino/plugin/bin/R8-win/Debug/*/Owly.rhp rhino/plugin/bin/R8-win/Debug/*/Owly.rui
```
Every folder that holds an `Owly.rhp` must also hold an `Owly.rui`. If not, STOP and report the layout.

### 5. CI check

In `.github/workflows/build.yml`, change
```yaml
        run: test -f net48/Owly.rhp && test -f net8.0/Owly.rhp
```
to
```yaml
        run: test -f net48/Owly.rhp && test -f net8.0/Owly.rhp && test -f net48/Owly.rui && test -f net8.0/Owly.rui
```

### 6. Docs

In `docs/owly/phase1-investigation.md`, line 77 is the bullet that starts "**Toolbar highlighting (Phase 4).** Nothing in the codebase touches toolbars". Append to it: `Plan 012 ships Owly.rui, a one-button toolbar loaded next to Owly.rhp; highlighting other toolbars' buttons is still Phase 4 work.`

### 7. Gates

Run the shared gates again.

## Done criteria

```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary   # succeeded, 0 errors
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary   # succeeded, 0 errors
dotnet test tests/Server.Tests/Server.Tests.csproj                          # all pass
test -f rhino/plugin/bin/R8-win/Debug/net48/Owly.rui && test -f rhino/plugin/bin/R8-win/Debug/net8.0/Owly.rui && test -f rhino/plugin/bin/R9-win/Debug/net8.0/Owly.rui && echo rui-staged   # rui-staged
grep -c "{{" rhino/plugin/Owly.rui                                         # 0
grep -c "! _Owly<\|! _OwlyWelcome<" rhino/plugin/Owly.rui                  # 2
git diff --stat HEAD -- art/                                               # empty
git grep -n "Owly.rui" -- .github/workflows/build.yml                      # 1 hit
```
Plus the three PowerShell checks from step 3 (well-formed, no placeholder, bytes equal).

## Manual check (by the user, Rhino 9 first, then Rhino 8)

1. Build (the "Rhino 9" launch configuration does this) and start Rhino.
   - **Expected:** an "Owly" toolbar docked at the top, with one button showing the owl and the word "Owly".
2. Left-click: the Owly panel opens. Left-click again: it closes. Right-click: the welcome popup opens.
3. Hover: the tooltip reads "Owly – your Rhino tutor".
4. Switch the theme to dark, restart, and check the dark owl is shown.
5. Report what you see, and which of these fallbacks applies:
   - **No toolbar at all:** check for `Owly.rui` next to the loaded `Owly.rhp` (`rhino/plugin/bin/R9-win/Debug/net8.0/`). Also run `_Toolbar`, open Rhino's toolbar files list, and see whether "Owly" is listed (it may be hidden: tick it).
   - **Toolbar shows but the owl is blank or a "?":** Rhino's SVG renderer doesn't draw embedded PNGs. The fix is that the team supplies an SVG export of the owl (`art/owly-small.svg` / `art/owly-small-dark.svg`), and the executor embeds that SVG's markup directly in `<light>` / `<dark>` in place of the `<image>` wrapper. The button still works meanwhile, because its text says "Owly".
   - **No "Owly" text next to the image:** `item_display_style="image_and_text"` isn't Rhino's value. Report it; the next step is to set the button to "image and text" once in Rhino's toolbar editor and copy that attribute value back into the file.
   - Rhino keeps its own copy of a plug-in's toolbar after the first load. Note where it appeared (look under `%APPDATA%\McNeel\Rhinoceros\9.0\UI\`). A changed `Owly.rui` may not show until that copy is removed.

## Tests

None automated, besides the CI check in step 5.

## Maintenance

- **When the owl art changes,** re-run step 3 against the template from step 2 (restore the placeholders first), or replace the two base64 strings. The `.rui` holds its own copy of the art.
- Keep the GUIDs. Changing them makes Rhino treat the toolbar as a new one, leaving the old one in students' profiles.

## STOP and report if

- The `.rui` doesn't end up next to every `Owly.rhp` (step 4).
- The XML isn't well-formed, or the embedded bytes don't equal the art files (step 3).
- The `Owly` or `OwlyWelcome` command is missing (step 1).
