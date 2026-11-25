# StandardWasm contributor rules

- Preserve names supplied by the user. Use alternative place or project names only on request.
- Accept compiled .NET Standard libraries as input. Keep the browser runtime independent of Blazor.
- Keep metadata loading, IL translation, module emission and browser integration separate. Use descriptive names and small cohesive modules.
- Comment invariants, stack transitions, control-flow assumptions and CLR-to-Wasm semantic differences. Keep straightforward code readable through naming.
- Add an ordinary C# library case for each new compiler capability. Compare the same assembly on .NET and Wasm. Verify browser claims in a real browser.
- Diagnose unsupported operations explicitly. Never silently approximate checked arithmetic, exception handling, initialization or object lifetime.
- Use standardized WebAssembly features where they implement the required semantics. Identify engine requirements and measure performance claims.
- Keep self-hosting dependencies visible. Only mark self-hosting complete after a generated compiler recompiles itself and passes the fixtures.
- Commit source, reproducible build inputs and documentation. Exclude downloaded dependencies, generated modules and build intermediates.
## Working together

Claude and Codex are equal engineering partners. Read GOAL.md, fetch main, then check open issues, PRs and docs/decisions/. Use GitHub issues and PRs for coordination so another session can reconstruct the work.

1. Review another agent's pending PR first, then unblock a dependency or claim an independent issue. Identify your role in coordination comments. Post a claim with UTC time, branch, scope, touched areas and acceptance checks. Re-read the issue after posting. The earliest valid claim takes precedence. Agree on interfaces before taking overlapping work.
2. Use a separate checkout or worktree and branch per active task, named codex/<issue>-<slug> or claude/<issue>-<slug>. For example, from the repository root run `git worktree add ../standardwasm-issue-12 -b codex/12-byte-reader origin/main`. Run npm ci in the new worktree.
3. For new work, open an issue describing the capability, dependencies and a reproducible acceptance test. Record shared interface or architecture decisions under docs/decisions/. Put runnable examples under demos/stage-NN-name/ and follow the demo index.
4. Open a PR linking the issue. Give the exact commands, results, engine versions, known limitations and remaining work. The author posts READY followed by the full tested head SHA. A different agent reviews that revision and posts APPROVE or REQUEST CHANGES followed by its full SHA and reasons. Use role-prefixed comments when sharing a GitHub account.
5. Merge through the PR once READY and independent APPROVE match the current head, the branch is up to date with main, checks pass and conflicts are resolved. Recheck immediately before merging and use an expected-head guard, e.g. `gh pr merge <number> --merge --match-head-commit <sha>`. New commits require fresh attestations. Keep main free of direct and force pushes. These attestations are a contributor procedure.
6. While waiting, take independent work. Before yielding, record the branch, head, last passing check, blocker and next action on the issue or PR. Resolve technical disagreements with a focused experiment. Bring mission, access, spending and destructive changes to the owner.
