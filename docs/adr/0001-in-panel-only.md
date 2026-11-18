# Owly is in-panel only; the external MCP surface is removed

RhinoAI let external AI clients (Claude Desktop, Cursor, the Claude Code plugin) drive Rhino. This went through a stdio router (`rhino/router`), an unauthenticated localhost MCP endpoint (`/`), the `MCPConnect`, `MCPStart`, `MCPStatus` and `MCPSpawn` commands, and an installer that edits other apps' config files. Owly is a tutor that is used only from its own chat panel. It must never act on the model, so an external door into Rhino's tools has no purpose. It is also a security risk: any local process can call tools on `/`. We removed the router, the `/` endpoint, those commands, the installer, and the `cc-plugin/`, `connector/` and Hugo `docs/` site along with their workflows and tests. Only the in-panel `/agent` route remains.

## Consequences

- `tests/Router.Tests` and `tests/Integration.Tests` were deleted with the router. End-to-end testing now has to be done by hand inside Rhino until a replacement exists.
- Bringing back external access would mean restoring this code from git history (before plan 003).
