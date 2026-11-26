// Acceptance checks for stage 88, kept apart from the runner so they can be exercised on synthetic rows.
// Returns a list of failure messages; an empty list means the stage passes.
//
// Invariants:
// - Every model B run must end with a matching final live-list checksum.
// - A run exhausts memory if and only if its strategy is a ceiling case.
// - A ceiling run must stop with fewer completed allocations than requested.
// - Every other run must complete its full requested churn. The 60-second cap in pressure-core.mjs is a
//   safety stop, not a result: a run it stops is an incomplete benchmark and fails the stage.
export function checkResults(byEngine) {
    const failures = [];
    for (const [label, all] of byEngine) {
        for (const r of all.b)
            if (!r.liveIntact) failures.push(`${label} ${r.strategy}: final live-list checksum mismatch`);
        for (const r of [...all.c, ...all.b]) {
            const name = `${label} ${r.strategy} ${r.pattern}`;
            const ceiling = r.strategy.includes("ceiling");
            if (Boolean(r.exhausted) !== ceiling) failures.push(`${name}: unexpected exhaustion state`);
            if (ceiling) {
                if (!(r.completedMiB < r.requestedMiB)) failures.push(`${name}: ceiling did not stop allocation`);
            } else if (r.timedOut) {
                failures.push(`${name}: stopped at 60 s after ${r.completedMiB} of ${r.requestedMiB} MiB (incomplete benchmark)`);
            } else if (r.completedMiB !== r.requestedMiB) {
                failures.push(`${name}: incomplete churn, ${r.completedMiB} of ${r.requestedMiB} MiB`);
            }
        }
    }
    return failures;
}

// Deterministic check of the validator itself, run before the real results are judged.
export function selfTestCheckResults() {
    const row = (strategy, extra) => ({ strategy, pattern: "p", requestedMiB: 200, completedMiB: 200,
        exhausted: false, timedOut: false, liveIntact: true, ...extra });
    const run = (c, b = []) => checkResults([["T", { c, b }]]);
    const cases = [
        ["complete rows pass", run([row("baseline"), row("x ceiling", { exhausted: true, completedMiB: 4 })]), 0],
        ["timed-out partial row fails", run([row("baseline", { timedOut: true, completedMiB: 1 })]), 1],
        ["timed-out row fails even if complete", run([row("baseline", { timedOut: true })]), 1],
        ["incomplete row fails", run([row("baseline", { completedMiB: 199 })]), 1],
        ["ceiling without exhaustion fails", run([row("x ceiling")]), 2],
        ["unexpected exhaustion fails", run([row("baseline", { exhausted: true })]), 1],
        ["model B checksum mismatch fails", run([], [row("baseline", { liveIntact: false })]), 1],
    ];
    for (const [name, failures, expected] of cases)
        if (failures.length !== expected)
            throw Error(`check-results self-test "${name}": expected ${expected} failures, got ${JSON.stringify(failures)}`);
    return cases.length;
}
