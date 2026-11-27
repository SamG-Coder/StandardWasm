export function oracleCases(oracle) {
    // Older worktrees publish a plain fixture array with the byte payloads inline.
    if (Array.isArray(oracle)) return oracle;
    const blobs = oracle.blobs.map(bytes => ({ bytes }));
    return oracle.cases.map(fixture => ({
        ...fixture,
        inputs: fixture.inputs.map(input => typeof input?.bytes === "number" ? blobs[input.bytes] : input)
    }));
}
