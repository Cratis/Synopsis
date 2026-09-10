# Synopsis — project context

Synopsis turns the executable examples scattered through a repository into the
clearest account of what the system actually promises. A .NET tool
(`Cratis.Synopsis.Tool`) that discovers Cratis-style specifications and renders
them as living documentation.

## Repository rules

### Specifications

# Specification conventions

Specs communicate behavior to both the runner and Synopsis. Use a `for_<subject>` folder, a `when_<action>`
class or `describe`, optional `given` reusable contexts, and one focused `should_<outcome>` fact / `it` for each
observable outcome. Put setup in `Establish` / `beforeEach`, the trigger in `Because`, and assertions only in the
then members. Names should read as natural language after underscores are replaced with spaces.

Parser specs must include realistic source text and assert the language-neutral result, not implementation
details of Roslyn or the TypeScript scanner.

### Git and pull requests

# Git and pull request conventions

Use imperative commit subjects that explain one coherent change. Do not include generated reports, build
artifacts, prompt transcripts, or secrets. Pull request descriptions become release notes: keep their Added,
Changed, Fixed, Removed, Security, and Deprecated bullets short, user-facing, and free of unverified issue
references. Run the repository gates before claiming a change is complete.

The repository-local `add-parser` skill lives under `.agents/skills/`.

## AI-assisted development

This repository uses the Cratis AI contract:

- **`.cratis/ai.json`** records the subscription — `cratis/documentation` plus the `cratis/engineering/csharp` maintainer cell.
- **`.cratis/PROJECT.md`** (this file) is the canonical project context; the root `AGENTS.md`, `CLAUDE.md`, and `GEMINI.md` are minimal bootstraps that point here and do nothing else.
- There is **no local AI corpus and no generated tool adapters** in this repository. Shared skills arrive through the Cratis AI marketplace plugins (Claude Code, Codex, GitHub Copilot, Cursor, and Pi are installable today — see the [harness guide](https://www.cratis.io/ai/harnesses/)).

For contributors:

1. Install the Cratis plugin for your harness once (per the harness guide); the subscribed profiles' skills then load automatically when tasks match.
2. General, reusable improvements are proposed in [`Cratis/AI`](https://github.com/Cratis/AI) — never copied into, or synchronized from, this repository.
3. Repository-specific facts and conventions belong in this file; repository-local skills live under `.agents/skills/`.
4. AI session work records (plans, handovers, session notes, scratch analyses) stay in the untracked `.ai-work/` folder and never enter git; a durable follow-up becomes a GitHub issue.
