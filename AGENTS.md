Read and follow all instructions in CLAUDE.md in this repository's root.

# UltimaOnline project instructions

- This checkout is the Abadoria server foundation based on `modernuo/ModernUO`.
- Use `https://github.com/souvenirexpress/ModernUO.git` as `origin` and keep official ModernUO as fetch-only `upstream`.
- Never push, force-push, merge, or publish without an explicit user request.
- Put shard-specific game systems in `Projects/UOContent/` and avoid `Projects/Server/` unless the user explicitly requests an engine change.
- The universal material/state/interaction system lives in `Projects/UOContent/Engines/WorldSimulation/`; its material data lives in `Distribution/Data/world-simulation/`.
- Preserve legacy item behavior unless an integration is explicitly documented and covered by tests.
- Run `dotnet build ModernUO.slnx --no-restore` and the relevant tests after C# changes.
- Do not use Ollama or a local model helper for this repository; verify every diff and test result in the current session.
