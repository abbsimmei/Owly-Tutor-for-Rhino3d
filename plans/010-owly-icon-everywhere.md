# 010: Owly's owl icon everywhere

- **Written against:** `17f9ef6` on `rhino-9.x`. Apply it after plan 009, which edits the same panel files.
- **Status:** TODO. **Blocked** until the four owl PNGs are committed (step 1).
- **Effort:** M–L
- **Risk:** Medium. Rhino's panel-icon and plug-in-icon loaders are only partly documented, and the art has to reach C# and the web panel.
- **Phase:** PRD Phase 3, bullet "Implement Owly's icon"

## Why

Owly still shows RhinoAI's artwork: the panel tab, the Options page, the plug-in icon and the yak package icon. PRD §3.3 asks for "an Owly owl icon in Rhino's interface", and the product owner asked for the owl **everywhere, as much as possible**. The team drew the art. **Never create, redraw, recolour or edit it.** Your job is only to load the files and display them.

The art, in `art/`:

| File | Size | Use |
|---|---|---|
| `owly.png` | 512×512 | large, light mode |
| `owly-dark.png` | 512×512 | large, dark mode |
| `owly-small.png` | 64×64 | small (16–32 px on screen), light mode |
| `owly-small-dark.png` | 64×64 | small, dark mode |

**The transparency rule.** Today's files are opaque: the light ones have a white background and the dark ones a black one, with no alpha. The product owner is keeping them like that for now, but the team wants to swap in transparent versions later **by replacing only the files**. So every path in this plan must keep the PNG's alpha channel:
- PNG streams only.
- Never convert to JPEG, BMP or a 24-bit format.
- Never paint the art onto a filled background.
- No CSS background, filter, blend mode or rounded clipping on the owl element.
- No wrapper whose only job is to be a coloured backdrop for the owl.

Where the owl appears after this plan:
1. The Rhino panel tab (light or dark by theme). This also fixes the old bug where both constants in `RhinoAIPlugin.cs:12-13` pointed at the same icon.
2. Rhino Options → Owly page icon.
3. The plug-in icon (`PlugInDescription(Icon)`), shown in the Plug-in Manager.
4. The yak package icon (`icon.png` next to the plug-in).
5. The settings dialog's window icon.
6. Panel header, before the "Owly" chip.
7. Panel empty state, both "ready" and "not set up", at a large size.
8. One small "Owly" head line at the start of each answer in the transcript.
9. The status strip while Owly is thinking (replaces the ✦ sparkle).
10. The `ask_user` question card header (replaces the "?" glyph).

The welcome popup (plan 011) and the toolbar button (plan 012) reuse what this plan builds.

## Repo conventions you must follow

- A new C# file written by an AI agent is named `*.ai.cs`.
- **Localization IDs.** New strings get the next unused ID. At `17f9ef6` the highest is 265. Find the current maximum with:
  ```sh
  git grep -hoE "LocalizeString\(\"[^\"]*\", [0-9]+\)" -- rhino/plugin | grep -oE "[0-9]+\)$" | tr -d ')' | sort -n | tail -1
  ```
- **Panel strings** need a C# twin in `rhino/plugin/UI/PanelStrings.ai.cs` and an English entry in `rhino/panel/src/i18n/strings.ts`.
- After changing `rhino/panel/src`, run `cd rhino/panel && npm install && npm run build` and commit `dist/panel.host.html`.
- PNGs are stored in **Git LFS** (`.gitattributes:2`). Don't change `.gitattributes`.
- `#if R9 … UseSdk` code and `UI/Pages/*` belong to the dormant SDK path (ADR-0003). Touch them only where this plan says so, so they don't crash once the old art is gone.
- Don't rename namespaces, classes or the csproj (ADR-0004).

## Scope

**Add:**
- `rhino/plugin/UI/OwlyArt.ai.cs`
- `rhino/panel/src/ui/owl.ts`

**Edit (C#):**
- `rhino/plugin/RhinoAI.csproj`
- `rhino/plugin/RhinoAIPlugin.cs`
- `rhino/plugin/Properties/AssemblyInfo.cs`
- `rhino/plugin/UI/AIOptionsPage.cs`
- `rhino/plugin/UI/AISettingsDialog.cs`
- `rhino/plugin/UI/Pages/AIAgentDefinitionsPage.cs` (icon loading only)
- `rhino/plugin/UI/Pages/AIPluginPage.cs` (`GetIcon` only)
- `rhino/plugin/UI/PanelStrings.ai.cs` (one new key)

**Edit (panel):**
- `src/globals.d.ts`
- `src/ui/header.ts`, `src/ui/empty.ts`, `src/ui/transcript.ts`, `src/ui/app.ts`, `src/ui/question.ts`, `src/ui/icons.ts`
- `src/i18n/strings.ts`, `src/styles/panel.css`
- `tools/verify.mjs`
- `dist/panel.host.html` (rebuilt)

**Delete:**
- `art/Panel_dark.ico`, `art/Panel_light.ico`, `art/Panel_dark.svg`, `art/Panel_light.svg`
- `art/logo.svg`, `art/logo-dark.svg`, `art/logo.afdesign`, `art/copy.svg`, `art/paperclip.svg`
- `rhino/plugin/icon.png`

**Out of scope. Do not touch:**
- The four `art/owly*.png` files: no edits, no re-encoding, no resizing on disk.
- `AssemblyInfo.cs`'s other `PlugInDescription` lines (McNeel address and URLs). That's a separate clean-up.
- `rhino/sdk/**`. Its `PlugInRegistry.cs:185-205` only reads the attribute name.
- `.gitattributes`, the CI workflow.
- Composer and chat behaviour.

## Steps

### 1. Prerequisite: the art is committed

```sh
git ls-files art/owly.png art/owly-dark.png art/owly-small.png art/owly-small-dark.png
git lfs ls-files | grep owly
```
Both commands must list all four files. If they don't, STOP and report: "commit art/owly*.png through Git LFS first." At `17f9ef6` they exist only as untracked files in the user's checkout, so an isolated worktree won't have them. Don't create placeholder images.

Then run the baseline: the shared gates from `plans/README.md` and `cd rhino/panel && npm install && npm run typecheck && npm test && npm run verify`. If it fails, STOP.

### 2. Embed the art; stop embedding the RhinoAI art

**`rhino/plugin/RhinoAI.csproj`:**
- Line 68 currently reads:
  ```xml
    <EmbeddedResource Include="../../art/*.svg;../../art/*.ico" />
  ```
  Replace it with:
  ```xml
    <!-- Owly's owl (plan 010). Resource names: Rhino.AI.owly.png, Rhino.AI.owly-dark.png, Rhino.AI.owly-small.png, Rhino.AI.owly-small-dark.png -->
    <EmbeddedResource Include="../../art/owly*.png" />
  ```
- In the `StageManifest` target, line 107 currently reads:
  ```xml
    <Copy SourceFiles="$(MSBuildProjectDirectory)/icon.png" DestinationFolder="$(PluginOutputDir)" SkipUnchangedFiles="true" />
  ```
  Replace it with this. The yak package icon becomes the large light owl, and `manifest.yml`'s `icon: icon.png` stays as it is.
  ```xml
    <Copy SourceFiles="$(MSBuildProjectDirectory)/../../art/owly.png" DestinationFiles="$(PluginOutputDir)/icon.png" SkipUnchangedFiles="true" />
  ```
- Delete `rhino/plugin/icon.png`.

### 3. `rhino/plugin/UI/OwlyArt.ai.cs` (new)

Create it with this content. It is the only C# code that opens the art.
```csharp
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
```
If `new Eto.Drawing.Icon(1f, bitmap)` or `new Eto.Drawing.Bitmap(image, w, h, ImageInterpolation)` doesn't compile against the Eto version referenced here, look for the matching constructor in the Eto API (`Icon(float scale, Bitmap)` / `Bitmap(Image, int?, int?, ImageInterpolation)`). If neither exists, STOP and report. `Reflection` is used by `typeof(...).Assembly`; remove the `using System.Reflection;` line if the compiler reports it unused.

Build R9 and R8. Then list the embedded resources from the net48 build:
```powershell
[System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Resolve-Path "rhino/plugin/bin/R8-win/Debug/net48/Owly.rhp")).GetManifestResourceNames() | Sort-Object
```
Expected: `Rhino.AI.Agents.Definitions.json` (or similar), `Rhino.AI.owly-dark.png`, `Rhino.AI.owly-small-dark.png`, `Rhino.AI.owly-small.png`, `Rhino.AI.owly.png`, `Rhino.AI.panel.html`, plus any localization XML. No `Panel_*` and no `logo*`. If the PNG names differ (for example the hyphens are mangled), STOP and report the actual names; don't rename the art.

### 4. C# icon sites

**4a. Panel tab: `rhino/plugin/RhinoAIPlugin.cs`.**
- Delete the two constants `IconResourceName` / `DarkIconResourceName` (lines 12-13) and the `LoadPanelIcon()` method (lines 40-58).
- Change the registration (line 20) to the resource-ID overload, which exists in RhinoCommon 8 and 9 as `RegisterPanel(PlugIn, Type, string, Assembly, string, PanelType)`:
  ```csharp
            Panels.RegisterPanel(this, typeof(UI.AIPanel), Localization.LocalizeString("Owly", 1),
                typeof(RhinoAIPlugin).Assembly, OwlyArt.ResourceName(OwlyArtSize.Small, OwlyArt.DarkTheme), PanelType.PerDoc);
  ```
- Remove `using System.IO;` and `using System.Reflection;` if they become unused.
- **Fallback, only if the manual check shows no icon on the tab:** register with the `System.Drawing.Icon` overload instead:
  ```csharp
    private static System.Drawing.Icon? PanelIcon()
    {
        try
        {
            using System.Drawing.Bitmap bitmap = OwlyArt.DrawingBitmap(OwlyArtSize.Small);
            return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        }
        catch
        {
            return null;
        }
    }
  ```
  Report which variant you shipped.

**4b. Options page: `rhino/plugin/UI/AIOptionsPage.cs`.**
- Delete the two SVG constants (12-13) and `LoadIcon` (43-55).
- Change `PageImage` to:
  ```csharp
    // Mac's Settings UI lists pages by icon; a page with no PageImage never shows in the navigation.
    public override Image PageImage => HostUtils.RunningInDarkMode switch
    {
        true => DarkCachedImage ??= OwlyArt.DrawingBitmap(OwlyArtSize.Small, dark: true),
        _ => LightCachedImage ??= OwlyArt.DrawingBitmap(OwlyArtSize.Small, dark: false),
    };
  ```
- Remove the usings that become unused (`System.IO`, `System.Reflection`).

**4c. Plug-in icon: `rhino/plugin/Properties/AssemblyInfo.cs`** line 15. Change it to:
```csharp
[assembly: PlugInDescription(DescriptionType.Icon, "Rhino.AI.owly-small.png")]
```

**4d. `rhino/plugin/UI/Pages/AIPluginPage.cs`** (SDK-only page) `GetIcon` (lines 212-220). The current code `new System.Drawing.Icon(stream)` throws on a PNG. Replace the body so it handles both formats:
```csharp
    private Bitmap? GetIcon(System.Reflection.Assembly assembly, string iconResource)
    {
        if (string.IsNullOrEmpty(iconResource)) return null;
        using Stream? stream = assembly.GetManifestResourceStream(iconResource);
        if (stream is null) return null;

        if (iconResource.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            return new System.Drawing.Icon(stream).ToBitmap().ToEto();

        MemoryStream copy = new();
        stream.CopyTo(copy);
        copy.Position = 0;
        return new Bitmap(copy);   // PNG: Eto keeps the alpha channel
    }
```
Delete the `// TODO : Method to get an icon from a .ico` comment above it. Check that `Bitmap` in this file is `Eto.Drawing.Bitmap`, since the old code returned `.ToEto()`; if it is ambiguous, write `Eto.Drawing.Bitmap`.

**4e. `rhino/plugin/UI/Pages/AIAgentDefinitionsPage.cs`** (SDK-only) lines 608-628. Delete the two `Preferred*IconResourceName` constants. Replace the getter and the loader with:
```csharp
            return HostUtils.RunningInDarkMode
                ? PreferredDarkImage ??= OwlyArt.EtoBitmap(OwlyArtSize.Small, 32, dark: true)
                : PreferredLightImage ??= OwlyArt.EtoBitmap(OwlyArtSize.Small, 32, dark: false);
```
Delete `LoadPreferredImage` and its comment.

**4f. Settings dialog: `rhino/plugin/UI/AISettingsDialog.cs`.** In the constructor, after `Resizable = true;` (line 17), add:
```csharp
        Icon = OwlyArt.WindowIcon();
```

Verify: `git grep -n "Panel_light\|Panel_dark\|logo\.svg\|logo-dark" -- rhino` prints nothing. Build R9 and R8.

### 5. Delete the RhinoAI art

First confirm nothing references each file:
```sh
git grep -n -E "Panel_(light|dark)|logo(-dark)?\.svg|logo\.afdesign|copy\.svg|paperclip\.svg" -- . ':!plans' ':!docs'
```
Expected: nothing. Then `git rm` the nine files listed under Delete in Scope. If the grep finds a live reference, STOP and report it.

### 6. Panel: the owl component

**6a. `src/globals.d.ts`.** Add:
```ts
/** esbuild inlines imported PNGs as data URLs (build.mjs loader), so an import is its URL. */
declare module '*.png' {
  const url: string;
  export default url;
}
```

**6b. `src/ui/owl.ts` (new).**
```ts
import { el } from '../core/dom.js';
import type { Child } from '../core/dom.js';
import type { PanelContext } from './context.js';
import owlyLarge from '../../../../art/owly.png';
import owlyLargeDark from '../../../../art/owly-dark.png';
import owlySmall from '../../../../art/owly-small.png';
import owlySmallDark from '../../../../art/owly-small-dark.png';

/**
 * Owly's owl (plan 010). The team's art is used as-is; replacing art/owly*.png with transparent
 * PNGs must need no code change, so never give `.owl` a background, filter, blend mode or rounded
 * clipping, and never wrap it in a coloured backdrop.
 *
 * Decorative: it always sits next to text that already says "Owly", so it is hidden from screen readers.
 */
export function owl(ctx: PanelContext, size: number, extraClass?: string): Child {
  const small = size <= 32;
  return el('img', {
    class: extraClass ? `owl ${extraClass}` : 'owl',
    alt: '',
    'aria-hidden': 'true',
    draggable: 'false',
    width: size,
    height: size,
    src: () =>
      ctx.store.scheme() === 'dark'
        ? small ? owlySmallDark : owlyLargeDark
        : small ? owlySmall : owlyLarge,
  });
}
```
The import path goes from `rhino/panel/src/ui/` up four levels to the repo root's `art/`. esbuild's `.png` loader is already `dataurl` (`build.mjs:28`), so nothing is fetched at runtime. If `npm run typecheck` rejects the `el('img', …)` props (for example `width` as a number), pass `String(size)`.

**6c. CSS (`src/styles/panel.css`).** After the `.icon { flex: none; display: block; }` rule (line 204), add:
```css
/* Owly's owl (plan 010). No background, filter, blend mode or rounded clipping: the art's own alpha must show. */
.owl { flex: none; display: block; object-fit: contain; }
```

### 7. Panel: placements

**7a. Header (`src/ui/header.ts`).** Make the owl the header's first child, before `agentButton`:
```ts
  return el(
    'header',
    { class: 'header' },
    owl(ctx, 20, 'header-owl'),
    agentButton,
    …
```
Import `owl` from `./owl.js`. If the header looks cramped at the 264 px dev-frame width, add `.header-owl { margin-inline-start: 2px; }`. Do **not** add a background.

**7b. Empty state (`src/ui/empty.ts`).**
- Ready branch: replace `icon('sparkle', 17),` (line 24) by moving a large owl **above** the title, as the first child of `.empty`: `owl(ctx, 64, 'empty-owl'),`. The `.empty-title` div keeps only its text span.
- Not-set-up branch (line 45): `el('div', { class: 'empty-title' }, icon('agent', 17), el('span', …))`. Remove `icon('agent', 17)` and put `owl(ctx, 64, 'empty-owl')` as the first child of that `.empty` too.
- CSS: delete `.empty-title .icon { color: var(--accent); }` (around line 1095) if no `.icon` remains in `.empty-title`.

**7c. Answer head (`src/ui/transcript.ts`, `turnView`).** Insert this between the `msg-user` line and the `each(() => turn.blocks(), …)` call (lines 90-91):
```ts
    when(
      () => turn.blocks().length > 0,
      () => el('div', { class: 'agent-head' }, owl(ctx, 16), el('span', { text: () => t('turn.agentName') })),
    ),
```
CSS, next to `.msg-agent` (around line 432):
```css
.agent-head { display: flex; align-items: center; gap: 5.4px; font-size: var(--fs-sm); font-weight: 600; color: var(--text-dim); }
```
New string `turn.agentName` = `Owly`:
- `strings.ts`: `'turn.agentName': 'Owly',`
- `PanelStrings.ai.cs`: `["turn.agentName"] = Localization.LocalizeString("Owly", <next id>),`, using the next unused ID from the grep in Repo conventions (266 at `17f9ef6`).

**7d. Status strip (`src/ui/app.ts:53`).** Replace `el('span', { class: 'spark' }, icon('sparkle', 14)),` with `owl(ctx, 16, 'spark'),`. In `panel.css` (lines 315-325), replace the `.status-strip .spark …` rules, the `.spark path` rules and `@keyframes twinkle` with:
```css
.status-strip .owl.spark { animation: owl-bob 1.9s ease-in-out infinite; }
@keyframes owl-bob {
  0%, 100% { opacity: .55; transform: translateY(0); }
  50% { opacity: 1; transform: translateY(-1px); }
}
```
In the existing `@media (prefers-reduced-motion: reduce)` block near line 852, add `.status-strip .owl.spark { animation: none; }`. The strip's own `background: var(--accent-soft)` is the strip's colour, not a backdrop for the owl; leave it.

**7e. Question card (`src/ui/question.ts:233`).** Replace `el('span', { class: 'glyph', 'aria-hidden': 'true' }, icon('question', 12)),` with `owl(ctx, 16),`. `overlay(ctx, …)` already has `ctx`; pass it down if the header is built in a helper without `ctx`. Delete the `.ask-head .glyph` CSS rule (lines 776-785) if nothing else uses `.glyph` inside `.ask-head`. Leave `.attach .glyph`, which is a different element.

**7f. Unused icons (`src/ui/icons.ts`).** After 7b–7e, check `grep -rn "icon('sparkle'\|icon('agent'\|icon('question'" src`. Delete each `ICONS` entry that has no remaining use. `IconName` is also referenced as a type from `src/protocol/events.ts` and possibly `mockHost.ts`; `npm run typecheck` will flag a string literal that names a deleted icon. Keep the icon in that case.

### 8. `tools/verify.mjs`: owl checks

Add a reusable page-side check near the other helpers at the top of the file:
```js
// Plan 010: the owl must never sit on a backdrop of our own making, so transparent art just works.
const owlsClear = (scope) => page.evaluate((sel) => {
  const owls = [...document.querySelectorAll(sel)];
  const bad = owls.filter((o) => {
    const s = getComputedStyle(o);
    return !(s.backgroundColor === 'rgba(0, 0, 0, 0)' || s.backgroundColor === 'transparent')
      || s.backgroundImage !== 'none' || s.filter !== 'none' || s.mixBlendMode !== 'normal' || s.borderRadius !== '0px';
  });
  return { count: owls.length, bad: bad.length };
}, scope);
```
If `page` isn't in scope where helpers are declared, define it inside the main flow just before its first use. Then add these checks:
- After `check('a settled tool card is one plain line', …)` (around line 153), where the seeded conversation is on screen:
  ```js
  const owlsSeeded = await owlsClear('.header .owl, .agent-head .owl');
  check('the owl shows in the header and on each answer, with nothing painted behind it',
    owlsSeeded.count >= 2 && owlsSeeded.bad === 0, JSON.stringify(owlsSeeded));
  ```
- After `check('a new conversation shows the empty state', …)` (around line 297):
  ```js
  const owlEmpty = await owlsClear('.empty .owl');
  check('the empty state shows the owl', owlEmpty.count === 1 && owlEmpty.bad === 0, JSON.stringify(owlEmpty));
  ```
- Where the test first sees an `.ask` card (around line 309):
  ```js
  const owlAsk = await owlsClear('.ask-head .owl');
  check('the question card shows the owl', owlAsk.count === 1 && owlAsk.bad === 0, JSON.stringify(owlAsk));
  ```
- Where the test sees `.status-strip` while thinking (around line 190):
  ```js
  const owlStrip = await owlsClear('.status-strip .owl');
  check('the thinking strip shows the owl', owlStrip.count === 1 && owlStrip.bad === 0, JSON.stringify(owlStrip));
  ```
Line numbers are from `17f9ef6`, before plan 009. Find the `check(` lines by their text.

### 9. Build and verify

```sh
cd rhino/panel && npm install && npm run build && npm run typecheck && npm test && npm run verify && npm run verify:shots; cd ../..
```
Look at the screenshots `verify:shots` writes, in light and dark: the owl is visible in the header, on each answer and in the empty state. Commit the rebuilt `dist/panel.host.html`. Then run the shared C# gates.

## Done criteria

```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary   # succeeded, 0 errors
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary   # succeeded, 0 errors
dotnet test tests/Server.Tests/Server.Tests.csproj                          # all pass
(cd rhino/panel && npm run typecheck && npm test && npm run verify)         # all pass, including the 4 new owl checks
git grep -n -E "Panel_(light|dark)|logo(-dark)?\.svg|logo\.afdesign|copy\.svg|paperclip\.svg" -- . ':!plans' ':!docs'   # nothing
ls art/                                                                    # exactly: owly.png owly-dark.png owly-small.png owly-small-dark.png
git diff --stat 17f9ef6 -- art/owly.png art/owly-dark.png art/owly-small.png art/owly-small-dark.png   # empty (art untouched)
test -f rhino/plugin/bin/R9-win/Debug/icon.png && echo yak-icon-staged       # prints yak-icon-staged (Windows dev build)
git grep -n "owly.*\.png\|\.png.*owly" -- rhino/plugin ':!rhino/plugin/UI/OwlyArt.ai.cs'   # only RhinoAI.csproj (embed + yak copy) and AssemblyInfo.cs: OwlyArt is the only C# loader of the art
```
Plus the resource listing from step 3.

## Manual check (by the user)

Do this in Rhino 9 and Rhino 8, each in **light and dark** theme (Rhino Options → Appearance). Restart Rhino after switching theme: the tab icon is chosen at load.
1. The Owly panel tab shows the owl. If it shows no icon, report it; the executor switches to the step 4a fallback.
2. **First open docks right.** On a fresh profile, or after resetting the window layout, run `Owly`. The panel opens docked on the right-hand side. If Rhino docks it elsewhere, report it. That is Rhino's default container and needs a separate decision.
3. Rhino Options shows the Owly page with the owl icon. On Mac, the page appears in Settings at all.
4. Plug-in Manager (Options → Plug-ins): Owly's row shows the owl. If it shows a generic icon, report it. Rhino may only read `.ico` there, and the team would then supply an `owly.ico` exported from their own art.
5. The panel's gear opens "Owly Settings" with the owl in its title bar (Windows).
6. In the panel: owl in the header; large owl in the empty state; a small "🦉 Owly" head above each answer; the owl gently pulsing in the thinking strip; the owl on a question card (ask "Should I use Loft or Sweep for a curved roof? Ask me questions first.").
7. **Transparency drill.** A teammate (not the executor) temporarily replaces all four `art/owly*.png` with transparent PNGs exported from their own art, rebuilds (C# and panel), and repeats checks 1–6. Expected: no white or black square anywhere. Restore the files afterwards unless they decide to keep the transparent ones.

## Tests

- 4 new verify checks (step 8). No C# tests: icon loading is Rhino UI and isn't linked into Server.Tests.

## Maintenance

- **Swapping the art:** replace the four files under `art/` with the same names and sizes, rebuild the C# plug-in and the panel (`npm run build`), and commit `dist/panel.host.html`. No code changes. The toolbar's `.rui` (plan 012) holds its **own copy** of the button image and must be re-imported separately.
- The panel-tab icon is chosen once, at plug-in load, so a theme switch shows the other variant only after a restart. That matches Rhino's own behaviour for plug-in panels.
- Never add `background`, `filter`, `mix-blend-mode` or `border-radius` to `.owl`. The verify checks will fail if you do.

## STOP and report if

- Step 1: the art isn't committed.
- The embedded resource names differ from `Rhino.AI.owly*.png`.
- An Eto constructor named in step 3 doesn't exist.
- `System.Drawing.Bitmap` can't load the PNG on Mac (Options page crash). Report it; a Mac-specific fallback needs a decision.
- A panel unit test (`npm test`) fails because node can't load a `.png` import. Unit tests only import `src/core` and `src/state` at `17f9ef6`, so this shouldn't happen.
