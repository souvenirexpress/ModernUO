Read and follow all instructions in CLAUDE.md in this repository's root.

# Abadoria server instructions

- This checkout is the Abadoria server based on `modernuo/ModernUO`.
- Use `https://github.com/souvenirexpress/ModernUO.git` as `origin`.
- Use `https://github.com/modernuo/ModernUO.git` as fetch-only `upstream`; never push to it.
- Ordinary reviewed commits and pushes to the owner fork are authorized by the user on 4 October 2026. Force-pushes, main merges and history rewrites require explicit authorization.
- The user has given standing authorization to deploy completed, verified
  Abadoria changes automatically to the isolated test shard and then live using
  the repository's backup, checksum, health-check, and rollback gates.
- Put shard-specific game systems in `Projects/UOContent/` and avoid `Projects/Server/` unless an engine change is explicitly requested.
- The universal material/state/interaction system lives in `Projects/UOContent/Engines/WorldSimulation/`; its material data lives in `Distribution/Data/world-simulation/`.
- Preserve existing Abadoria changes and legacy behavior. Do not clean or reset the dirty worktree without explicit approval.
- Preserve legacy item behavior unless an integration is explicitly documented and covered by tests.
- Run `dotnet build ModernUO.slnx --no-restore` and the relevant tests after C# changes.
- Do not use Ollama or a local model helper. Perform the work in the current
  Codex session and verify every resulting diff and test directly.

## GitHub Delivery And Task Coordination

The owner explicitly requested this workflow on 4 October 2026. It authorizes
ordinary commits and pushes of reviewed Abadoria changes to the owner's forks,
and project coordination messages among Client, Server, Studio and Integration
tasks, including replies and handoffs. This does not authorize unrelated
messages, force-pushes, destructive Git operations or merging into main.

- At each substantive turn, confirm the actual repository root, branch,
  working-tree status, origin and upstream tracking. Read this section again
  before declaring work complete. Fetch origin before comparing branch state.
- Work on a named topic branch with its own Git worktree. Do not edit source in
  detached HEAD. Resolve paths from that worktree; do not silently switch to
  C:\UO-Mobile because a runbook hardcodes it. Two active tasks must not write
  the same working copy. Coordinate a handoff before touching shared files.
- Client, gateway, Studio, Admin, deployment and project documentation belong
  in https://github.com/souvenirexpress/abadoria.git. ModernUO source belongs
  in https://github.com/souvenirexpress/ModernUO.git. Its official upstream
  remains fetch-only. The ignored nested server checkout is a separate repo;
  parent commits never back up its changes. Give server work its own worktree.
- A completed change requires relevant checks, reviewed staged paths and diff,
  a focused commit, a normal push to origin and confirmation that the remote
  branch contains that exact HEAD. Set upstream on the first push. Do not
  stop at a local commit or deployment. No repeated push permission is needed.
- Stage explicit reviewed paths only. Preserve other tasks' changes. Never
  stage private UO assets, credentials, saves, logs or generated payloads.
  Existing unfinished changes need an owner and review before being included.
- Run the Abadoria Scripts/Test-GitHubDelivery.ps1 with -Repository ModernUO
  -RepositoryPath pointing to this server worktree after a
  push. Before packaging/deploying, use -Mode Deployment, which also requires
  a clean working tree. Check both repositories when a release affects both.
  Build the release from these published commits and record their SHAs with
  test results and artifact checksums. A GitHub failure blocks new deployment;
  report it and preserve local work. An emergency rollback may use an already
  verified published release. Existing deployment safety gates still apply.
- At task start, inspect the other project tasks' current status. Send a
  coordination message when ownership, shared files, packets, export formats
  or deployment overlap. Exchange branch, commit, checks and outstanding work
  at completion with the Integration task. Avoid repeated unchanged polling.
- The completion response includes branch, commit, tests, confirmed push and
  deployment status. If any step fails, explicitly state the incomplete step;
  never describe a dirty/unpublished deployment as fully delivered.
- Integration combines only handed-off, published, checked topic branches.
  Source-task acknowledgements precede integration of still-active work.
  Merging into main continues to require the owner's explicit instruction.
