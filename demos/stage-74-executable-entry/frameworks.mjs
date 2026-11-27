// Issue #299: framework resolution before compiling, as hostfxr does before Main.
// A framework-dependent publish names its shared frameworks in runtimeconfig.json. The CLR's
// host picks an installed version by the roll-forward policy; if none fits, it prints
// "You must install or update .NET to run this application." to stderr, exits with
// 0x80008096 and never runs Main (CLR probe 3 on #299).
//
// In the browser there is no installed runtime. A framework is available here when the
// host can supply it:
//   - Microsoft.NETCore.App is built in. The compiler lowers the platform members it supports
//     by identity (Console, String and friends) and reports any other member it reaches.
//   - Other frameworks (Microsoft.WindowsDesktop.App) need their runtime-pack DLLs. A later
//     resolver can download and cache those packs; none are supplied yet.

export const frameworkMissingExitCode = 0x80008096 | 0; // -2147450730 as a signed process status

export const builtInFrameworks = Object.freeze([{ name: "Microsoft.NETCore.App", version: "10.0.10" }]);

function parseVersion(text) {
    const parts = String(text).split("-")[0].split(".").map(Number);
    return parts.length >= 2 && parts.every(Number.isInteger) ? [parts[0], parts[1], parts[2] ?? 0] : null;
}

function compare(a, b) {
    for (let i = 0; i < 3; i++) if (a[i] !== b[i]) return a[i] - b[i];
    return 0;
}

// The frameworks runtimeconfig.json requests: runtimeOptions.framework or .frameworks.
export function requestedFrameworks(config) {
    const options = config?.runtimeOptions ?? {};
    const list = Array.isArray(options.frameworks) ? options.frameworks : options.framework ? [options.framework] : [];
    return list.map(entry => ({ name: entry.name, version: entry.version, rollForward: options.rollForward ?? entry.rollForward ?? "Minor" }));
}

// hostfxr's default roll-forward (Minor): the same major version, at least the requested
// minor and patch; the lowest such minor, then its highest patch. LatestPatch keeps the
// minor; Major allows a higher major; Disable needs the exact version.
function selectVersion(requested, candidates, policy) {
    const want = parseVersion(requested);
    if (!want) return null;
    const versions = candidates.map(parseVersion).filter(Boolean).filter(have => compare(have, want) >= 0);
    let fitting;
    if (policy === "Disable") fitting = versions.filter(have => compare(have, want) === 0);
    else if (policy === "LatestPatch") fitting = versions.filter(have => have[0] === want[0] && have[1] === want[1]);
    else if (policy === "Major" || policy === "LatestMajor") fitting = versions;
    else fitting = versions.filter(have => have[0] === want[0]); // Minor, LatestMinor
    if (fitting.length === 0) return null;
    fitting.sort(compare);
    const minor = policy === "LatestMinor" || policy === "LatestMajor" ? fitting[fitting.length - 1] : fitting[0];
    const patches = fitting.filter(have => have[0] === minor[0] && have[1] === minor[1]);
    return patches[patches.length - 1].join(".");
}

// { ok: true, frameworks: [{ name, version }] } or { ok: false, missing: { name, version } }.
export function resolveFrameworks(config, available = builtInFrameworks) {
    const resolved = [];
    for (const request of requestedFrameworks(config)) {
        const candidates = available.filter(entry => entry.name === request.name).map(entry => entry.version);
        const version = selectVersion(request.version, candidates, request.rollForward);
        if (!version) return { ok: false, missing: { name: request.name, version: request.version }, available };
        resolved.push({ name: request.name, version });
    }
    return { ok: true, frameworks: resolved };
}

// The stderr text hostfxr writes, modelled for the browser host. The first line and the
// framework line are exact; the app path and locations name this host instead of a disk.
export function frameworkMissingMessage({ missing, available }, appName, architecture = "x64") {
    const found = available.length
        ? available.map(entry => "  " + entry.version + " (" + entry.name + ", supplied by the StandardWasm host)").join("\r\n")
        : "  (none)";
    return "You must install or update .NET to run this application.\r\n\r\n" +
        "App: " + appName + "\r\n" +
        "Architecture: " + architecture + "\r\n" +
        "Framework: '" + missing.name + "', version '" + missing.version + "' (" + architecture + ")\r\n" +
        ".NET location: StandardWasm browser host\r\n\r\n" +
        "The following frameworks were found:\r\n" + found + "\r\n";
}
