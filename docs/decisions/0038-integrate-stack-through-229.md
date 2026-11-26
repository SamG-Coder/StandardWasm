# 0038 · Land the stacked PRs through #229 as one integration merge

By 2026-11-26 about seventy stacked PRs (#51 through #229, plus the side
branches #93 → #153 → #155 and #86 → #139) were waiting on each other. Each
was based on the previous PR's branch, and main had moved on independently.
Landing them one at a time needed roughly seventy refresh, review and merge
cycles.

The owner first approved collapsing each approved segment into the branch
below it. Then, citing "its taking to long to merge all / cant we just merge
all then test everything", the owner chose **"Up to #229, skip #231
(Recommended)"**:

- Build one integration branch from the #229 head (`e374cf1`).
- Merge in the newer heads that #229 did not contain:
  - the #64 and #79 fixes
  - the re-reviewed #188, #208 and #225 heads
  - the side branches #93/#153/#155 and #139
- Merge in current main and resolve the conflicts.
- Run the full suite and the stage demos that `npm test` does not cover.
- Land everything as a single PR into main, then close the stacked PRs as
  superseded.

This is a one-time deviation from the per-PR READY/APPROVE step in
AGENTS.md. The individual PRs were reviewed at their own heads, and most
were approved. The integration PR is tested as a whole, and Codex
re-attests it after it merges. Open review points do not block the merge.
They become issues instead:

- #147: a null-receiver `NewDelegate` traps under `catch (Exception)`.
- #181: neither of the two type-forwarding guards has a fixture.
- #205: the platform check in `ValueBase` has no fixture.
- the unreviewed #64 and #79 fixes

#231 stays out of the integration because of its 4,096-entry cap defect.
It, and the PRs stacked above it, are rebased onto main separately.

## Integration choices

- **Branch IL kinds.** The stack assigns kinds 46 through 59, and #139
  used 46 for `MathCall`. The integration gives `MathCall` kind 60, so
  every kind stays unique.
- **Oracle format.** The stack's shared-blob format (`scripts/oracle.mjs`)
  replaces main's browser-only image deduplication. `oracleCases` reads
  both the plain array and the blob form.
- **Assembly reader.** For `ArrayTypeRefs`, `MetadataTableRows`,
  `ObjectLayouts` and `PlatformAssemblyIdentity`, the stack's version
  supersedes the copy on main. Its added parameters default to main's
  behaviour, so main's callers are unchanged.
- **Verify.** `Verify/Program.cs` and `Verify/Verify.csproj` keep both
  sides' cases and fixture references.
