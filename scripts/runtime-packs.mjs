import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";
import { run } from "./toolchain.mjs";

// Decision 0044: CoreLib bodies are lowered only from a CoreLib built for a
// 4-byte native width. On Windows the installed win-x86 runtime supplies one,
// and running an oracle on that host makes the 32-bit CLR the contract.
function coreRuntimes(host) {
    const listed = execFileSync(host, ["--list-runtimes"], { encoding: "utf8" });
    return new Map([...listed.matchAll(/^Microsoft\.NETCore\.App (10\.\S+) \[(.+)\]$/gm)]
        .map(([, version, directory]) => [version, join(directory, version)]));
}

// The newest .NET 10 version installed for both win-x86 and the default host.
export function wasm32Runtime() {
    const host = process.env.STANDARDWASM_X86_DOTNET || "C:/Program Files (x86)/dotnet/dotnet.exe";
    if (!existsSync(host)) throw Error("Install the win-x86 .NET 10 runtime or set STANDARDWASM_X86_DOTNET.");
    const narrow = coreRuntimes(host);
    const wide = coreRuntimes("dotnet");
    const version = [...narrow.keys()].filter(v => wide.has(v)).sort().at(-1);
    if (!version) throw Error("Install matching win-x86 and default-host .NET 10 runtimes.");
    return { host, version, directory: narrow.get(version), widePack: wide.get(version) };
}

// Build a framework-dependent oracle with the default SDK, then run it on the
// 32-bit CLR at the selected version.
export function runOn32BitClr(runtime, project, dll, args) {
    run("dotnet", ["build", "-c", "Release", project]);
    run(runtime.host, ["exec", "--fx-version", runtime.version, dll, ...args]);
}
