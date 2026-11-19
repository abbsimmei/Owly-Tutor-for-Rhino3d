# 016: Spike: flash the toolbar button on Rhino 9 (public APIs only)

- **Written against:** `6571b16` on `rhino-9.x`. **Requires plan 015 to be DONE.**
- **Status:** DONE (not feasible). The in-Rhino probe on 2026-11-19 printed `service: NULL`: the public `RhinoUiServiceLocator` doesn't supply `IToolbarsService` in a running Rhino 9. Part B is not built. The PRD note is added.
- **Effort:** M, time-boxed. Part A is at most one working session. If Part A says "not feasible", the plan ends there.
- **Risk:** Medium. It depends on undocumented R9 interfaces.
- **Phase:** PRD Phase 4, "Implement tool highlighting" (PRD §3.2: "a clear visual indication (e.g. blinking, colour change or outline)"). Decision record: [ADR-0007](../docs/adr/0007-panel-ui-actions-on-student-click.md).

## Why

Plan 015 shows the student a **location card** for a command's toolbar button. The PRD also wants the button itself to blink. RhinoCommon has no documented API for that. The product owner agreed to a **time-boxed R9 spike under one hard rule: public types only**. If reaching a button's on-screen control needs reflection into non-public Rhino types, or into private members of public types, the spike ends "not feasible", the finding is recorded, and **no code ships**.

## Facts you need

- In R9's `Rhino.UI.dll` (RhinoCommon package `9.0.26132.12305-wip`, referenced in `rhino/plugin/RhinoAI.csproj`) these are public but undocumented:
  - interface `Rhino.UI.Runtime.IToolbarsService`, with members including `UseNewStuff()` and `GetToolbar(Guid)`, which returns `IToolbar`
  - interface `IToolbar`, with members including `Id`, `FileId` and `GetControl(uint, bool)`, which returns `Eto.Forms.Control`
- No public way to **obtain** an `IToolbarsService` instance is known yet. That is what Part A looks for.
- None of this exists in R8 (8.19).
- After plan 015, `AIPanelViewModel.FindToolbar(name, turnId)` gets `ToolbarPlace`s from `ToolbarLocator`. Each place has `GroupId`; add toolbar and item ids if you need them, in `ToolbarIndex.ai.cs`, with tests.
- ADR-0007 allows a temporary on-screen flash drawn by Owly when the student clicks. It does not allow changing the toolbar itself: no restyling, no showing or hiding.

## Scope

**Part A (always):**
- Add `docs/owly/toolbar-flash-spike.md`.
- You may use a throwaway local command for experiments, but **do not commit it**.

**Part B (only if A says feasible):**
- Add `rhino/plugin/UI/ToolbarFlash.ai.cs`.
- Edit `rhino/plugin/UI/AIPanelViewModel.ai.cs` (one call in `FindToolbar`).
- If item ids are needed, edit `rhino/plugin/UI/ToolbarIndex.ai.cs` and `tests/Server.Tests/ToolbarIndexTests.ai.cs`.

**Out of scope:**
- any `System.Reflection` use on Rhino types beyond reading **public** members found by public discovery
- `BindingFlags.NonPublic`
- `Rhino.UI.Internal.*`
- changing toolbar visibility, style or layout
- R8 and Mac implementations

## Part A: investigation

1. In the referenced R9 `Rhino.UI.dll`, list how `IToolbarsService` can be obtained through public surface only. Use a reflection dump of **public** types and members in a scratch console app outside the repo, or an IL viewer. Look for:
   - public static properties or methods returning `IToolbarsService`
   - a public service locator, for example `Rhino.Runtime.HostUtils`, `RhinoApp`, `Rhino.UI.RhinoEtoApp`, or any `GetService`/`ServiceProvider` on public types
   - a public type that implements it and has a public constructor or `Instance`
2. If you find one, check the rest of the chain in a debug session in Rhino 9:
   1. `GetToolbar(toolbarGuid)` on a visible toolbar
   2. `GetControl(…)`: find out what the `uint` and `bool` mean by trying values and recording the results
   3. a control for one button, with `Bounds`
   4. its screen position: `control.PointToScreen` or `Eto.Forms.Control.Bounds` plus the parent window's location
3. Write `docs/owly/toolbar-flash-spike.md`:
   - the Rhino 9 build used
   - each link in the chain, marked public or non-public
   - what worked
   - the final verdict, **FEASIBLE** or **NOT FEASIBLE**, with the reason
4. **If NOT FEASIBLE:**
   - Set this plan's status to `DONE (not feasible)` in `plans/README.md`, with one line of reason.
   - In `PRD.md`'s Phase 4 tool-highlighting note from plan 017, add "(on-screen flash not possible with Rhino's public API; see docs/owly/toolbar-flash-spike.md)".
   - STOP.

## Part B: only if FEASIBLE

1. Add `rhino/plugin/UI/ToolbarFlash.ai.cs`, with `internal static class ToolbarFlash { public static void Show(Guid toolbarId, Guid itemId) }`. Wrap the class body in `#if R9 … #else` with a no-op `Show` in the `#else` branch. `rhino/plugin/RhinoAI.csproj:24` already defines `$(RhinoTarget)` (`R9` or `R8`) as a compile constant, so no build changes are needed. It must:
   - Resolve the button's screen rectangle with the Part A chain. Any failure (null, exception) returns silently with an `[owly]` debug log.
   - Show a borderless, topmost, non-activating Eto `Form` that doesn't take focus or clicks (for example `ShowActivated = false`, `Topmost = true`, `WindowStyle = None`, transparent background where the platform allows it). It is sized to the rectangle plus 3 px, with a 2–3 px outline in the accent colour `#478CE6`.
   - Blink 3 times over about 1.5 s with an `Eto.Forms.UITimer`, then close and dispose.
   - Never run twice at once: a new call closes the previous flash first.
2. In `AIPanelViewModel.FindToolbar`, after posting `ToolbarFoundEvent`, call `ToolbarFlash.Show(...)` for the **first visible** place only. Hidden groups never flash, and nothing is made visible.
3. Run the shared gates. The R8 build must still succeed, and R8 behaviour must be unchanged.
4. Manual check in Rhino 9:
   - Clicking the toolbar icon on `_Loft` flashes the real Loft button 3 times, then disappears.
   - Focus stays where it was, and typing in the Owly composer still works.
   - Clicking it on a command whose toolbar is hidden flashes nothing, and the toolbar stays hidden.
   - The flash is in the right place when Rhino's window is on a second monitor with different DPI.

   If the position is wrong on scaled displays and can't be fixed with public Eto APIs, remove Part B, record it, and mark NOT FEASIBLE.

## Done criteria
- `docs/owly/toolbar-flash-spike.md` exists with a verdict.
- `git grep -n "NonPublic\|Rhino.UI.Internal" -- rhino/plugin` prints nothing. Use `git grep`, because a plain `grep -r` also matches compiled DLLs under `bin/`.
- If FEASIBLE: the R9 and R8 builds succeed, Server.Tests passes, and the Part B manual check is recorded in `plans/README.md`.
- If NOT FEASIBLE: no source files changed. Only the spike doc, the README status and the PRD note change.

## Maintenance note
These interfaces are undocumented, so any Rhino 9 update can break them. Every step must therefore fail silently, leaving the location card from plan 015 working. Re-check this spike after each Rhino 9 service release, and before Rhino 9 leaves WIP.
