# Claude Code CLI is Owly's only AI backend

RhinoAI's panel could run the Claude Code CLI or the Codex CLI, and partly supported Gemini. It also includes an unused direct-API SDK (`rhino/sdk`). For the MVP we keep only the Claude Code CLI path, which already works end to end. We remove Codex, Gemini and agent switching. Every extra agent is another launch configuration that has to be locked down and tested against the "Only Help" restrictions. We also launch Claude with `--setting-sources ""`, so a student's own Claude settings and hooks don't leak into Owly's agent, and we removed the user-configurable extra MCP servers, which were pre-approved and would get around the tool restrictions.

## Consequences

- Each student needs the Claude Code CLI installed and a Claude subscription or API key. This is a cost and setup barrier for a course.
- `rhino/sdk` is left untouched and dormant. If students need key-based access without a CLI, wiring the panel to the SDK is the next option to consider. That would be a new ADR.
