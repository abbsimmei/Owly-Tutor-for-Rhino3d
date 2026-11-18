# Rename only user-visible identifiers so Owly loads alongside built-in RhinoAI

Rhino 9 ships McNeel's RhinoAI plugin, and Owly is a fork of it. Rhino refuses to load two plugins with the same GUID, and panels and commands must also be unique. So Owly needs its own plugin GUID and assembly name (done in `b5e89d3`), a new panel GUID, the command `Owly` (formerly `AI`), the panel title "Owly" and the yak package `Owly` (formerly `Rhino-MCP-Platform`). Owly still builds for both Rhino 8 and Rhino 9.

We deliberately keep the `RhinoAI.csproj` file name, the `Rhino.AI` namespace, class names such as `RhinoAIPlugin`, and the `[RhinoAI]` log prefixes. A full rename would touch hundreds of files for no change in behaviour, and would make it harder to pull fixes from McNeel's upstream later.

## Consequences

- The plugin GUID is written by hand in two places that must match: `Properties/AssemblyInfo.cs` and `AISettings.cs`.
- Readers will see "RhinoAI" in code. That is intentional; user-facing text says "Owly".
