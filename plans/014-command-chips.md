# 014: Command chips: click a command to copy it

- **Written against:** `6571b16` on `rhino-9.x` (plans 002-013 applied).
- **Status:** DONE (`50204fe`, see `plans/README.md`)
- **Effort:** M
- **Risk:** Low. Panel rendering plus one new panel command; the clipboard path already works.
- **Phase:** PRD Phase 4, bullets "Implement command recommendations", "Implement command copy", "Implement command terminal highlight". Decision record: [ADR-0007](../docs/adr/0007-panel-ui-actions-on-student-click.md).

## Why

Owly's prompt already writes every Rhino command as inline code with a leading underscore, for example `` `_Loft` `` (`rhino/plugin/Agents/AgentPrompts.cs:42-48`). Today that renders as a plain `<code>` element. The product owner decided:

1. Every `` `_Name` `` in an answer becomes a **clickable chip with a copy symbol**. Other inline code (`` `layer_name` ``) and fenced code blocks stay exactly as they are.
2. **One click** does four things:
   - copies the name, with the underscore (`_Loft`), to the clipboard
   - turns the copy symbol into a tick for 1.2 s
   - moves keyboard focus to Rhino's main window
   - shows a hint notice: "Copied _Loft. Paste it on the command line with Ctrl+V, then press Enter." On macOS it says ⌘V.
3. **Nothing is ever typed into Rhino's command line** (no `RhinoApp.SendKeystrokes`), and nothing runs. The student pastes and presses Enter.
4. The host does **not** check that the command exists. The prompt already makes the AI verify every command with `get_commands`. The host does check that the text *looks like* a command name, so nothing else can reach the clipboard this way.

Glossary terms (see `GLOSSARY.md`): **Command copy**, **Command-line highlight**.

## Facts you need

- Each answer's markdown is **rebuilt from scratch about once per animation frame while it streams** (`rhino/panel/src/ui/message.ts:21-29`):
  ```ts
  bind(() => {
    text();
    if (queued) return;
    queued = true;
    requestAnimationFrame(() => {
      queued = false;
      host.replaceChildren(renderMarkdown(text.peek(), options));
    });
  });
  ```
  So the chip must be created **inside `renderMarkdown`** from its options. A listener attached afterwards would be lost on the next chunk. The fenced-code copy button works the same way (`rhino/panel/src/core/markdown.ts:122-131`):
  ```ts
  if (options.copy) {
    const button = element('button', 'md-code-copy') as HTMLButtonElement;
    button.type = 'button';
    button.textContent = 'Copy';
    button.addEventListener('click', () => {
      options.copy?.(code);
      button.textContent = 'Copied';
      setTimeout(() => { button.textContent = 'Copy'; }, 1200);
    });
    caption.appendChild(button);
  }
  ```
- Inline code is rendered at `markdown.ts:75-78`:
  ```ts
  if (code !== undefined) {
    const node = element('code');
    node.textContent = code;
    fragment.appendChild(node);
  }
  ```
- `core/` must not import from `ui/`. Icons live in `rhino/panel/src/ui/icons.ts`, where `icon(name: IconName, size = 16): SVGElement` already has `copy` and `check`. Pass the icons in through the options instead of importing them in `markdown.ts`.
- The clipboard path today: `ctx.copy(text)` sends `{ type: 'clipboard.write', text }` (`rhino/panel/src/main.ts:28-33`). C# handles it at `rhino/plugin/UI/AIPanelViewModel.ai.cs:185` and `:405-409`:
  ```csharp
  private static bool CopyToClipboard(string text)
  {
      Clipboard.Instance.Text = text;
      return true;
  }
  ```
- The panel can raise its own notice, as `rhino/panel/src/ui/composer.ts:33-37` does:
  ```ts
  store.apply({ type: 'notice', level: 'error', text: t('composer.overAttachmentLimit', ...) });
  ```
- The platform is in `store.host()` (`rhino/panel/src/state/store.ts:92`, set on `hello`). `HostInfo.platform` is `'windows' | 'macos'` (`rhino/panel/src/protocol/events.ts:157`).
- Panel strings have an English fallback in `rhino/panel/src/i18n/strings.ts` (`EN`) **and** a C# copy for Rhino's translators in `rhino/plugin/UI/PanelStrings.ai.cs`, each with a unique localization ID:
  ```csharp
  ["turn.copy"] = Localization.LocalizeString("Copy the reply", 77),
  ```
  The highest ID used in `rhino/plugin` is **276**. Use **277 onwards**, and check first with `grep -rhoE "LocalizeString\([^;]*, *[0-9]+\)" rhino/plugin --include=*.cs | grep -oE "[0-9]+\)$" | tr -d ')' | sort -n | tail -1`.
- Panel commands in C#: `rhino/plugin/UI/PanelCommands.ai.cs` (partial record, attributes split across files):
  ```csharp
  [JsonDerivedType(typeof(PromptCommand), "prompt")]
  [JsonDerivedType(typeof(OpenImageCommand), "image.open")]
  [JsonDerivedType(typeof(SaveImageCommand), "image.save")]
  internal abstract partial record PanelCommand { }
  internal sealed record OpenImageCommand(string Id) : PanelCommand;
  ```
  The dispatch switch is in `AIPanelViewModel.IncomingCommand` (`AIPanelViewModel.ai.cs:166-190`). JSON is camelCase (`rhino/plugin/UI/PanelJson.cs`).
- The test pattern for commands is `tests/Server.Tests/PanelAttachmentTests.ai.cs:15-18`:
  ```csharp
  [Test]
  public void The_paper_clip_command_the_panel_sends_is_understood()
  {
      Assert.That(PanelJson.Deserialize("""{"type":"attachments.pick"}"""), Is.TypeOf<PickAttachmentsCommand>());
  }
  ```

## Repo conventions you must follow

- New C# files written by an AI agent are named `*.ai.cs`. Logging uses `RhinoApp.WriteLine("[owly] …")` or `HostUtils.LogDebugEvent`.
- `tests/Server.Tests/Server.Tests.csproj` links Rhino-free plugin files by explicit path. A new file the tests need must be added there and must not use RhinoCommon.
- After changing `rhino/panel/src`, rebuild and **commit** `rhino/panel/dist/panel.host.html`.
- Panel unit tests use `node:test` + `node:assert/strict` (example: `rhino/panel/src/state/format.test.ts`), with no DOM.

## Scope

**Add:**
- `rhino/panel/src/core/commandName.ts`
- `rhino/panel/src/core/commandName.test.ts`
- `rhino/plugin/UI/CommandName.ai.cs`
- `tests/Server.Tests/CommandCopyTests.ai.cs`

**Edit:**
- Panel: `rhino/panel/src/core/markdown.ts`, `rhino/panel/src/ui/message.ts`, `rhino/panel/src/ui/context.ts`, `rhino/panel/src/main.ts`, `rhino/panel/src/protocol/events.ts`, `rhino/panel/src/protocol/mockHost.ts`, `rhino/panel/src/i18n/strings.ts`, `rhino/panel/src/styles/panel.css`, `rhino/panel/tools/verify.mjs`, `rhino/panel/dist/panel.host.html` (rebuilt)
- C#: `rhino/plugin/UI/PanelCommands.ai.cs`, `rhino/plugin/UI/AIPanelViewModel.ai.cs`, `rhino/plugin/UI/PanelStrings.ai.cs`
- Tests: `tests/Server.Tests/Server.Tests.csproj`

**Out of scope, do not touch:**
- the toolbar location button (plan 015)
- `AgentPrompts.cs` and the PRD (plan 017)
- any agent tool under `rhino/plugin/Tools/`
- `RhinoApp.SendKeystrokes` or `RunScript`
- the fenced code-block copy button
- the turn-footer copy button
- the art

## Steps

### 1. Baseline
Run the shared gates from `plans/README.md`, then `cd rhino/panel && npm install && npm run typecheck && npm test && npm run build && npm run verify`. If any of them fails before you change anything, STOP and report.

### 2. Command-name rule, panel (`rhino/panel/src/core/commandName.ts`, new)
```ts
// A Rhino command as Owly writes it in an answer: `_Loft`, `_-Loft`, `_ExtrudeCrv` (plan 014).
// The leading underscore is required. Anything else (spaces, options, newlines) is not a command chip.
const COMMAND = /^_-?[A-Za-z][A-Za-z0-9_]*$/;

export function isCommandName(text: string): boolean {
  return COMMAND.test(text);
}
```
`commandName.test.ts`: accepted: `_Loft`, `_-Loft`, `_ExtrudeCrv`, `_Line2`. Rejected: `Loft`, `_`, `_-`, `_Loft Enter`, `_Loft\n`, `__Loft`, `_Loft;`, `_1Loft`, `layer_name`, `''`.

Note `__Loft`: after `_` the next character must be `-` or a letter, so `__Loft` is rejected. Check it in the test.

Verify: `cd rhino/panel && npm test`. All tests pass, including the new ones.

### 3. Renderer (`rhino/panel/src/core/markdown.ts`)
Extend the options:
```ts
export interface CommandChipOptions {
  /** Called with the command name (`_Loft`) when the student clicks the chip. */
  copy: (name: string) => void;
  /** Builds the chip's glyphs; passed in so core/ does not depend on ui/. */
  glyph: (name: 'copy' | 'check') => Node;
  /** Accessible label, e.g. "Copy _Loft". */
  label: (name: string) => string;
}

export interface MarkdownOptions {
  copy?: (text: string) => void;
  openLink?: (url: string) => void;
  /** When supplied, inline code that is a Rhino command (`_Loft`) renders as a copy chip. */
  command?: CommandChipOptions;
}
```
In `inline()`, replace the `code` branch with:
```ts
if (code !== undefined) {
  if (options.command && isCommandName(code)) fragment.appendChild(commandChip(code, options.command));
  else {
    const node = element('code');
    node.textContent = code;
    fragment.appendChild(node);
  }
}
```
And add:
```ts
function commandChip(name: string, command: CommandChipOptions): HTMLElement {
  const chip = element('span', 'md-cmd');
  const button = element('button', 'md-cmd-copy') as HTMLButtonElement;
  button.type = 'button';
  button.title = command.label(name);
  button.setAttribute('aria-label', command.label(name));
  const code = element('code');
  code.textContent = name;
  const glyph = element('span', 'md-cmd-glyph');
  glyph.appendChild(command.glyph('copy'));
  button.append(code, glyph);
  button.addEventListener('click', () => {
    command.copy(name);
    glyph.replaceChildren(command.glyph('check'));
    setTimeout(() => glyph.replaceChildren(command.glyph('copy')), 1200);
  });
  chip.appendChild(button);
  return chip;
}
```
Keep the `<code>` inside the button. `verify.mjs` counts `.msg-agent p code`, and the chip must still read as code.

The `md-cmd` wrapper `<span>` is there so plan 015 can add a second button next to it.

Verify: `npm run typecheck` is clean.

### 4. Wire it (`ui/context.ts`, `main.ts`, `ui/message.ts`, `protocol/events.ts`)
- `events.ts`, `PanelCommand` union: add `| { type: 'command.copy'; name: string }`. Put it next to `clipboard.write`, with the comment `// Plan 014: copy a Rhino command and focus Rhino. Never typed into the command line (ADR-0007).`
- `context.ts`: add `copyCommand(name: string): void;` to `PanelContext`.
- `main.ts`: implement it in `ctx`:
  ```ts
  copyCommand: (name) => {
    bridge.send({ type: 'command.copy', name });
    const mac = store.host.peek()?.platform === 'macos';
    store.apply({ type: 'notice', level: 'info', text: t(mac ? 'command.copiedMac' : 'command.copied', name) });
  },
  ```
  Import `t` from `./i18n/t.js`.
- `message.ts`: build the options once:
  ```ts
  const options: MarkdownOptions = {
    copy: ctx.copy,
    openLink: ctx.openLink,
    command: {
      copy: ctx.copyCommand,
      glyph: (name) => icon(name, 12),
      label: (name) => t('command.copyNamed', name),
    },
  };
  ```
  Import `icon` from `./icons.js`, `t` from `../i18n/t.js` and `type MarkdownOptions` from `../core/markdown.js`.

### 5. Strings
`rhino/panel/src/i18n/strings.ts` `EN`, next to `turn.copy`:
```ts
'command.copyNamed': 'Copy {0}',
'command.copied': 'Copied {0}. Paste it on the command line with Ctrl+V, then press Enter.',
'command.copiedMac': 'Copied {0}. Paste it on the command line with ⌘V, then press Enter.',
```
Add the same three to `rhino/plugin/UI/PanelStrings.ai.cs` after `turn.copy`, using the next free IDs (277, 278 and 279 if step 1's grep still says 276):
```csharp
["command.copyNamed"] = Localization.LocalizeString("Copy {0}", 277),
["command.copied"] = Localization.LocalizeString("Copied {0}. Paste it on the command line with Ctrl+V, then press Enter.", 278),
["command.copiedMac"] = Localization.LocalizeString("Copied {0}. Paste it on the command line with ⌘V, then press Enter.", 279),
```

### 6. Style (`rhino/panel/src/styles/panel.css`)
After the `.md code` rule (around line 473), add:
```css
/* Plan 014: a Rhino command in an answer is a copy chip. */
.md-cmd { display: inline-flex; vertical-align: baseline; }
.md-cmd-copy {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 0;
  border: 0;
  background: none;
  color: inherit;
  cursor: pointer;
  border-radius: var(--r-sm);
}
.md-cmd-copy code { cursor: pointer; }
.md-cmd-glyph { display: inline-flex; color: var(--text-dim); }
.md-cmd-copy:hover code { border-color: var(--accent-line); background: var(--accent-soft); }
.md-cmd-copy:hover .md-cmd-glyph { color: var(--accent); }
.md-cmd-copy:focus-visible { outline: 2px solid var(--accent-ring); outline-offset: 1px; }
```
Do not style `.owl` or touch any owl rule (`npm run verify` checks them).

### 7. Mock host (`rhino/panel/src/protocol/mockHost.ts`)
- In `send()`, next to `case 'clipboard.write':` (about line 836), add:
  ```ts
  case 'command.copy':
    void navigator.clipboard?.writeText(command.name);
    return;
  ```
- Line 364 reads `` '- `Loft` through the rotated rectangles gives the skin', ``. Change `` `Loft` `` to `` `_Loft` `` so the mock shows a chip. Change only that one line.

### 8. C# command (`rhino/plugin/UI/CommandName.ai.cs`, new; `PanelCommands.ai.cs`; `AIPanelViewModel.ai.cs`)
`CommandName.ai.cs` must stay Rhino-free, because it is linked into the tests:
```csharp
using System.Text.RegularExpressions;

namespace Rhino.AI.UI;

// The shape of a Rhino command as Owly writes it (plan 014): `_Loft`, `_-Loft`. The panel only makes chips
// from this shape; the host checks it again so nothing else can reach the clipboard through command.copy.
internal static class CommandName
{
    private static readonly Regex Shape = new("^_-?[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    public static bool IsValid(string? text) => text is not null && Shape.IsMatch(text);
}
```
`PanelCommands.ai.cs`: add the attribute `[JsonDerivedType(typeof(CopyCommandCommand), "command.copy")]` to the attribute list, and add the record:
```csharp
// Plan 014 / ADR-0007: copy a Rhino command name and focus Rhino. Never typed into the command line.
internal sealed record CopyCommandCommand(string Name) : PanelCommand;
```
`AIPanelViewModel.ai.cs`:
- Add a switch arm after `ClipboardCommand copy => ...`:
  ```csharp
  CopyCommandCommand command => CopyCommand(command.Name),
  ```
- Add the handler next to `CopyToClipboard`:
  ```csharp
  // Plan 014 / ADR-0007: the student clicked a command chip. Copy it and hand focus to Rhino so they can paste
  // it on the command line. Nothing is typed into Rhino and nothing runs.
  private bool CopyCommand(string name)
  {
      if (!CommandName.IsValid(name))
      {
          HostUtils.LogDebugEvent($"[owly] command.copy ignored a value that is not a command name.\n");
          return false;
      }

      CopyToClipboard(name);
      try
      {
          if (Document is { } doc) RhinoApp.SetFocusToMainWindow(doc);
          else RhinoApp.SetFocusToMainWindow();
      }
      catch (Exception ex)
      {
          HostUtils.LogDebugEvent($"[owly] could not focus Rhino after copying a command: {ex.Message}\n");
      }
      return true;
  }
  ```
  Do not log `name`. A value that failed validation could be anything.

If `RhinoApp.SetFocusToMainWindow(RhinoDoc)` does not compile on the R8 target, use the parameterless overload on both targets. Do not add `#if`.

Verify: the R9 build and the R8 build both succeed with 0 errors and no new warnings.

### 9. C# tests
- In `tests/Server.Tests/Server.Tests.csproj`, add `<Compile Include="../../rhino/plugin/UI/CommandName.ai.cs" LinkBase="PanelSources" />` to the PanelSources `ItemGroup`.
- Create `tests/Server.Tests/CommandCopyTests.ai.cs` (NUnit, namespace `Rhino.AI.Server.Tests`), following `PanelAttachmentTests`:
  - `{"type":"command.copy","name":"_Loft"}` deserialises to `CopyCommandCommand` with `Name == "_Loft"`.
  - `CommandName.IsValid` accepts `_Loft`, `_-Loft`, `_ExtrudeCrv`.
  - It rejects `Loft`, `_Loft Enter`, `"_Loft\n"`, `__Loft`, `""` and `null`.

Verify: `dotnet test tests/Server.Tests/Server.Tests.csproj`. All pass; the count is 148 plus your new tests.

### 10. Browser checks (`rhino/panel/tools/verify.mjs`)
Add checks to the **host-contract** section only. The mock streams `_Loft` only inside a scripted scenario that verify cancels early, so the mock section is not a reliable place for them.

Place the new block right after the existing rendered checks (about line 680, after the `check('the mcp__rhino__ prefix never reaches the panel', …)` group) and **before** the context-menu checks (about line 776). Don't clear `window.__sent`; search it with `.some(...)`.

1. Send one extra event built in the script. Do not edit `tests/host-events.json` for this plan:
   ```js
   await host.evaluate(() => window.rhinoAI.receive({ type: 'turn.text', turnId: 'turn-0', blockId: 'turn-0-cmd',
     delta: 'Use `_Loft` here. The layer is `layer_name`.' }));
   await wait(200);
   ```
   The store accepts text for a finished turn. Plan 015 relies on this block id.
2. Check that `.msg-agent .md-cmd-copy code` with text `_Loft` exists.
3. Check that a `code` element with text `layer_name` exists and has no `.md-cmd` ancestor (`!el.closest('.md-cmd')`).
4. Click `.md-cmd-copy`. Check that `window.__sent.some((m) => m.type === 'command.copy' && m.name === '_Loft')`.
5. Check that a `.notice.info` contains `_Loft`.
6. Check that the existing markdown checks (`inlineCode > 0`, `strayMarkers === 0`) still pass unchanged.

Copy the style of the existing `check(...)` calls.

Verify: `npm run build && npm run verify`. Every check passes, and there are no console errors.

### 11. Commit the rebuilt panel
`rhino/panel/dist/panel.host.html` must be the output of `npm run build` from this change.

## Done criteria
- The R9 and R8 builds succeed with 0 errors. `dotnet test tests/Server.Tests/Server.Tests.csproj` passes, with the new tests included.
- `cd rhino/panel && npm run typecheck && npm test && npm run verify` all pass.
- `grep -rn "SendKeystrokes" rhino/plugin` prints nothing.
- `git diff --stat` touches only the files listed in Scope, plus `plans/README.md` (status and the manual check).
- `git status` shows `rhino/panel/dist/panel.host.html` modified, and it is committed.

## Manual Rhino check (record the result in `plans/README.md`)
Rhino 9, then Rhino 8, via `.vscode/launch.json`. Ask "How do I loft between curves?".
1. `_Loft` renders as a chip with a copy symbol. Inline code that isn't a command, like a layer name, stays plain.
2. Click the chip. The symbol becomes a tick. The hint notice appears. Rhino's window has focus, so pressing Ctrl+V pastes `_Loft` on the command line without running it.
3. Press Enter. Loft starts; this is the student's own action.
4. Nothing in the document changed before Enter.
5. Click a chip **while another command is running** (for example during `_Line`). Only the clipboard changes; Rhino's prompt is untouched.

## Escape hatches
- If `Clipboard.Instance.Text` throws on a background thread, or the copy works in Rhino 9 but not Rhino 8, wrap the copy in `RhinoApp.InvokeOnUiThread` and note it. Do not change `ClipboardCommand`'s behaviour.
- If focusing the main window steals focus in a way that closes the panel or loses the transcript, remove the focus call, keep copy + hint, and STOP to report. The product owner decides.
- If the chip breaks the existing verify checks for inline code or stray markers, adjust the new markup, not the old checks.

## Maintenance note
Plan 015 adds a second button inside `.md-cmd` and a `locate` callback to `CommandChipOptions`. Plan 017 changes the prompt to tell the AI that commands become copy buttons. Any later "type into the command line" idea is rejected by ADR-0007.
