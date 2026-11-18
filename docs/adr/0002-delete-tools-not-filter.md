# Enforce "never act" by deleting tools, not filtering them

The PRD requires the no-modelling rule to be enforced "through technical tool permissions" as well as through prompts. RhinoAI exposed tools that run code (`run_python`, `run_csharp`), run commands (`run_command`), change the document or view, and drive Grasshopper. The only switch it had was a user-editable `DisabledTools` list. We **deleted** those tools and the code that only they used (`ScriptProjects/`, `RhinoCode/`, the Grasshopper helpers). We did not filter them. A tool that doesn't exist cannot be re-enabled by a setting, a prompt injection or a registry bug, and the shipped plugin gets smaller.

The AI-visible toolset is now `get_commands`, `get_context` (with its Grasshopper parts removed), `get_selection`, `list_objects` and `ask_user`.

## Considered options

- **An allow-list filter in `ToolRegistry`.** It's easier to reverse, but the dangerous code still ships, and a missed tool would be a silent hole.
- **Disabling the tools by default through `DisabledTools`.** The user can turn them back on, so this does not meet the PRD.

## Consequences

- Any new tool must be read-only. Adding one that changes Rhino state needs a new ADR that supersedes this one.
- `get_viewport_image` was removed as well, even though it looks read-only: it changes the display mode and camera and never restores them. As a side effect, viewport images are no longer sent to the AI provider.
