# 003: Remove the external MCP surface

- **Written against:** `ba4b4a0` (apply on top of plan 002)
- **Status:** TODO
- **Effort:** M
- **Risk:** Medium, because many files are deleted. The shared gates catch breakage.

## Why

Owly is used only from its in-Rhino chat panel (ADR `docs/adr/0001-in-panel-only.md`). The inherited code also lets *external* AI apps drive Rhino. It does this through:

- a router program (`rhino/router`);
- an unauthenticated localhost endpoint `/`, which any process on the machine can use to call every tool, including ones that run arbitrary code;
- the commands `MCPConnect`, `MCPStart`, `MCPStatus` and `MCPSpawn`;
- an installer that edits other apps' config files;
- three router-control tools named `_router_*`, which are visible to every agent.

None of this is needed for the panel, and all of it is attack surface. The panel's agent talks directly to `http://localhost:{port}/agent` and never uses the router.

## Scope

### Delete these (whole files or folders)

- `rhino/router/` (entire folder, including `codegen/`)
- `tests/Router.Tests/`, `tests/Integration.Tests/`, `tests/launcher/`, `shared/`
- `cc-plugin/`, `.claude-plugin/`, `connector/`
- `docs/content/`, `docs/layouts/`, `docs/static/`, `docs/assets/`, `docs/data/`, `docs/hugo.yaml`, `docs/go.mod`, `docs/go.sum`. **Keep `docs/owly/` and `docs/adr/`.**
- `.github/workflows/connector.yml`, `connector-release.yml`, `docs.yml`, `launcher.yml`
- `rhino/plugin/Commands/MCPConnectCommand.cs`, `MCPStartCommand.cs`, `MCPStatusCommand.cs`, `MCPSpawnCommand.cs`
- `rhino/plugin/McpClientConfigInstaller.ai.cs`, `McpConfigEdit.ai.cs`, `RouterStaging.ai.cs`, `RouterMcpConfig.ai.cs`, `RouterEnvOverride.ai.cs`
- `rhino/plugin/Internal/RouterControlTool.ai.cs` (and the `Internal/` folder, if it is then empty)
- `tests/Server.Tests/McpConfigEditTests.ai.cs`, `RouterStagingTests.ai.cs`, `RouterPathsTests.ai.cs`

### Edit these

- `rhino/plugin/McpServer.ai.cs`
- `rhino/plugin/RhinoAIHost.ai.cs`
- `rhino/plugin/RhinoAIPlugin.cs`
- `rhino/plugin/AIAutoLoad.cs`
- `rhino/plugin/Agents/AgentPrompts.cs`
- `rhino/plugin/RhinoAI.csproj`
- `tests/Server.Tests/Server.Tests.csproj`
- `rhino.slnx`
- `.github/workflows/build.yml`
- `.vscode/launch.json`, `.vscode/tasks.json`
- `CLAUDE.md`
- `README.md`

## Out of scope

- Any tool under `rhino/plugin/Tools/` (plan 004).
- Agents and Codex (plan 005).
- `rhino/sdk`, `rhino/acp`, `tests/sdk.tests` and `.github/workflows/sdk.yml`. Keep them.

## Steps

1. **Delete** everything in the "Delete these" list. Then run `git grep -lnE "RouterMcpConfig|RouterStaging|RouterEnvOverride|McpClientConfigInstaller|McpConfigEdit|MCPSpawnCommand|MCPStartCommand|MCPStatusCommand|MCPConnectCommand|RouterControlTool|RouterPaths" -- rhino tests` to list every file that still references the deleted code. Fix each one in the steps below.

2. **`rhino/plugin/McpServer.ai.cs`: serve only `/agent`.** Today it opens two listeners on the same port:
   ```csharp
   ExternalListener = Listen(port, ExternalRoute);
   AgentListener = Listen(port, $"{AgentRoute}/");
   _ = AcceptAsync(ExternalListener, new McpDispatcher(services, filtered: false), ExternalRoute);
   _ = AcceptAsync(AgentListener, new McpDispatcher(services, filtered: true), AgentRoute);
   ```
   - Remove `ExternalRoute`, `ExternalListener` and its accept loop.
   - Change `HasStarted` to `AgentListener is not null`.
   - Update `Stop()` / `Dispose()` so they only close the agent listener.
   - Keep `McpDispatcher`'s `filtered` parameter as it is. `McpEndpoint.ai.cs` stays unchanged in this plan.

3. **`rhino/plugin/RhinoAIHost.ai.cs`: remove the router announcements.**
   - Delete `WriteAnnouncement`, `WriteDeparture`, `ListenerDropDir`, the heartbeat machinery (`EnsureHeartbeat`, `StopHeartbeatIfIdle`, `Heartbeat`, `_lastAnnounceTick`, `_heartbeatHooked`, `HeartbeatIntervalMs`, `_clock`, if they become unused) and their call sites.
   - Keep `Start`, `Stop`, `StartOrRestart`, `TryGetPortFor` and `TryGetNextPort`. `AgentDispatch` uses them.

4. **`rhino/plugin/RhinoAIPlugin.cs`.**
   - Delete the `RouterStaging.EnsureStaged()` block (lines 19-22).
   - Delete the headless autostart block (lines 33-51), which reads `MCPSpawnCommand.PortEnvVar`.

5. **`rhino/plugin/AIAutoLoad.cs`.** Delete the `WasStartedViaAgent` property and the `if (WasStartedViaAgent) return true;` line.

6. **`rhino/plugin/Agents/AgentPrompts.cs:10`.** It uses `RouterMcpConfig.ServerName`. Add `private const string ServerName = "rhino";` to `AgentPrompts` and use it. The value must stay `"rhino"`, because `ClaudeStreamJsonParser.ai.cs` registers the server under the key `"rhino"`.

7. **`rhino/plugin/RhinoAI.csproj`.**
   - Delete the `RouterRids` properties and the Linux comment below them.
   - Delete `<Compile Include="../router/RouterPaths.ai.cs" Link="RouterPaths.ai.cs" />`.
   - Delete the whole `<Target Name="BundleRouter" ...>`.
   - Trim the long macOS/Linux comment that mentions `router/`.
   - If the csproj has an `AdditionalFiles` or `ProjectReference` to the router, delete it too.

8. **`tests/Server.Tests/Server.Tests.csproj`.** Delete the three `<Compile Include>` lines for `McpConfigEdit.ai.cs`, `RouterStaging.ai.cs` and `../../rhino/router/RouterPaths.ai.cs`.

9. **`rhino.slnx`.** Delete the `/router/` folder block and the `Router.Tests` and `Integration.Tests` project lines.

10. **`.github/workflows/build.yml`.** In the `test` job, delete the "Build router" and "Test router" steps. Keep "Test server". Change the comment `# Both projects are Rhino-target-agnostic` so it refers to Server.Tests only.

11. **`.vscode/`.**
    - `launch.json`: delete the "Router" and "Docs" configurations.
    - `tasks.json`: delete the "build router", "register router with claude", "pack connector" and "install connector" tasks.

12. **Docs.**
    - In `CLAUDE.md`, delete the commands that mention `Router.csproj` / `Router.Tests` and the router bullet. Change the last architecture bullet so it lists only what still exists.
    - Replace `README.md` with a short Owly README: the name, one sentence from `PRD.md` §1, links to `PRD.md` and `docs/owly/phase1-investigation.md`, and the build commands from `plans/README.md`.

## Done criteria

- The shared gates pass.
- `git grep -nE "RouterMcpConfig|RouterStaging|RouterEnvOverride|McpClientConfigInstaller|McpConfigEdit|MCPSpawn|MCPStart|MCPStatus|MCPConnect|_router_|RouterPaths|ExternalRoute" -- rhino tests .github .vscode` prints nothing.
- `test ! -d rhino/router && test ! -d cc-plugin && test ! -d connector && test -d docs/adr && test -d docs/owly` succeeds.
- `dotnet sln rhino.slnx list` (or reading the file) shows no router or integration-test projects.

## Tests

Three test files are deleted along with the code they covered. No new tests.

## Maintenance

Bringing back external MCP access would mean restoring the deleted code from git history. Write an ADR that supersedes ADR-0001 first.

## STOP and report if

- Something outside the listed files still needs the router or the `/` route to compile (for example, panel code calling `MCPStart`).
- `AgentDispatch` uses an API you removed from `RhinoAIHost`.
