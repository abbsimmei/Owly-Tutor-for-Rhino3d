# Owly replaces Claude Code's system prompt, and students can't edit it

RhinoAI passed its instructions with `--append-system-prompt`, so they were added after Claude Code's own default system prompt. That default prompt describes a software-engineering agent: file edits, git, code style and built-in tools that Owly launches without (`--tools ""`). It pulls the model towards doing things, which works against Only Help. Owly now launches the CLI with `--system-prompt` and supplies the whole prompt itself (`rhino/plugin/Agents/AgentPrompts.cs`). The free-text "Prompt:" box in settings is removed, and any value a student saved earlier is ignored, so the tutor's rules are the same for every student.

## Considered options

- **Keep appending.** Least change, but Owly's rules have to compete with an identity they contradict, and every turn pays for tokens that don't apply.
- **Keep a student or teacher prompt box.** It would let a course add instructions. We dropped it for now: it is one more way for the tutor's behaviour to drift between students, and nobody has asked for it yet.

## Consequences

- Owly loses Claude Code's generic guidance on using tools. `AgentPrompts.cs` must say everything the model needs to know about Owly's five agent tools.
- If a course later wants its own instructions, add them as a separate, clearly labelled setting that is appended after Owly's rules, and write an ADR that amends this one.
- The launch flags listed in `CLAUDE.md` now include `--system-prompt`.
