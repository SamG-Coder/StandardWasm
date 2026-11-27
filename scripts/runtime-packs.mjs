import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";
import { run } from "./toolchain.mjs";

// Decision 0044: CoreLib bodies are lowered only from a CoreLib built for a
// 4-byte native width. On Windows the installed win-x86 runtime supplies one,
// and running an oracle on that host makes the 32-bit CLR the contract.
function installedRuntimes(host, framework) {
    const listed = execFileSync(host, ["--list-runtimes"], { encoding: "utf8" });
    return new Map([...listed.matchAll(/^(\S+) (10\.\S+) \[(.+)\]$/gm)]
        .filter(([, name]) => name === framework)
        .map(([, , version, directory]) => [version, join(directory, version)]));
}

// Order .NET runtime versions by SemVer 2.0 precedence: numeric major, minor
// and patch, then a release above any of its prereleases, then prerelease
// identifiers left to right (numeric ones numerically, below alphanumeric).
// A plain string sort would put 10.0.10 before 10.0.9.
export function compareRuntimeVersions(left, right) {
    const parse = version => {
        const match = /^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?$/.exec(version);
        if (!match) throw Error("Unrecognized runtime version " + version);
        return { core: match.slice(1, 4).map(Number), pre: match[4]?.split(".") ?? [] };
    };
    const a = parse(left), b = parse(right);
    for (let i = 0; i < 3; i++) if (a.core[i] !== b.core[i]) return a.core[i] - b.core[i];
    if (!a.pre.length || !b.pre.length) return b.pre.length - a.pre.length;
    for (let i = 0; i < Math.min(a.pre.length, b.pre.length); i++) {
        const [x, y] = [a.pre[i], b.pre[i]];
        if (x === y) continue;
        const [nx, ny] = [/^\d+$/.test(x), /^\d+$/.test(y)];
        if (nx && ny) return Number(x) - Number(y);
        if (nx !== ny) return nx ? -1 : 1;
        return x < y ? -1 : 1;
    }
    return a.pre.length - b.pre.length;
}

// The newest .NET 10 version installed for both win-x86 and the default host.
function windowsX86Host() {
    const host = process.env.STANDARDWASM_X86_DOTNET || "C:/Program Files (x86)/dotnet/dotnet.exe";
    if (!existsSync(host)) throw Error("Install the win-x86 .NET 10 runtime or set STANDARDWASM_X86_DOTNET.");
    return host;
}

export function wasm32Runtime() {
    const host = windowsX86Host();
    const narrow = installedRuntimes(host, "Microsoft.NETCore.App");
    const wide = installedRuntimes("dotnet", "Microsoft.NETCore.App");
    const version = [...narrow.keys()].filter(v => wide.has(v)).sort(compareRuntimeVersions).at(-1);
    if (!version) throw Error("Install matching win-x86 and default-host .NET 10 runtimes.");
    return { host, version, directory: narrow.get(version), widePack: wide.get(version) };
}

export function win32DesktopRuntime(version) {
    const host = windowsX86Host();
    const desktop = installedRuntimes(host, "Microsoft.WindowsDesktop.App").get(version);
    const shared = installedRuntimes(host, "Microsoft.NETCore.App").get(version);
    if (!desktop || !shared) throw Error("Install the matching win-x86 .NET runtime and Windows Desktop runtime " + version + ".");
    return { host, version, desktop, shared };
}

// Build a framework-dependent oracle with the default SDK, then run it on the
// 32-bit CLR at the selected version.
export function runOn32BitClr(runtime, project, dll, args) {
    run("dotnet", ["build", "-c", "Release", project]);
    run(runtime.host, ["exec", "--fx-version", runtime.version, dll, ...args]);
}
