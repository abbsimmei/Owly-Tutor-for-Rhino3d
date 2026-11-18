# Owly

Owly is an AI-powered educational plugin for Rhino that helps architecture students understand and use Rhino's tools and commands, while the student always does the modelling. It is built on McNeel's RhinoAI.

- [PRD.md](PRD.md): product requirements
- [docs/owly/phase1-investigation.md](docs/owly/phase1-investigation.md): Phase 1 investigation

## Build

The .NET 8 SDK is required.

```sh
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R9 -clp:NoSummary
dotnet build rhino/plugin/RhinoAI.csproj -p:RhinoTarget=R8 -clp:NoSummary   # on Windows this also builds net48
dotnet test tests/Server.Tests/Server.Tests.csproj
```
