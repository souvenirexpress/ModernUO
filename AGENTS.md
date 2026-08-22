Read and follow all instructions in CLAUDE.md in this repository's root.

# Abadoria server instructions

- This checkout is the Abadoria server based on `modernuo/ModernUO`.
- Use `https://github.com/souvenirexpress/ModernUO.git` as `origin`.
- Use `https://github.com/modernuo/ModernUO.git` as fetch-only `upstream`; never push to it.
- Never push, force-push, merge, rebase, publish, or deploy without an explicit user request.
- Put shard-specific game systems in `Projects/UOContent/` and avoid `Projects/Server/` unless an engine change is explicitly requested.
- Preserve existing Abadoria changes and legacy behavior. Do not clean or reset the dirty worktree without explicit approval.
- Run `dotnet build ModernUO.slnx --no-restore` and the relevant tests after C# changes.
