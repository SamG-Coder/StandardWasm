# 0040 · Review boundaries for collapsed pull request stacks

The repository owner approved consolidating the existing stack in
[#51](https://github.com/SamG-Coder/StandardWasm/pull/51). This decision applies
to that consolidation and preserves the independent review in AGENTS.md.

- Within a contiguous segment of approved PRs, merge each independently
  attested head into its immediate parent from the top down. Use the literal
  reviewed SHA as the expected head. Fix any active request for changes before
  including that PR in a segment.
- Refresh the combined segment against current main once. Test the combined
  result in Node and Edge. The author posts READY with the full resulting SHA,
  and a different reviewer reviews that exact revision and posts the verdict
  with the same SHA. This checks the interaction between the segment's changes.
- Recheck the current main ancestry and both attestations before merging the
  segment into main through its PR with an expected-head guard. Advance to the
  next segment after the merge.

[#55](https://github.com/SamG-Coder/StandardWasm/pull/55) is the first combined
segment after #51. It incorporates #56, #59, and #61.
