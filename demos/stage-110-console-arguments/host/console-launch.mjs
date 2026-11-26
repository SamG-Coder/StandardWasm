import { packEntry, readManagedCompanions } from "../../stage-74-executable-entry/companions.mjs";
import { buildArguments, consoleImportModule, createConsoleHost, hex } from "./console-host.mjs";

// Compiles a dropped .NET entry with the self-hosted compiler and runs it
// with the console host of decision 0037. The compile half follows the
// Stage 74 launcher, which stays unchanged for its own parameterless entries.
// selection.arguments is the string[] passed to Main. selection.console
// names the Console.OutputEncoding and Environment.NewLine the host models.
const decoder = new TextDecoder("utf-8", { fatal: true });

function readManifest(asset) {
    if (!asset) throw Error("Provide the original .NET deployment manifests.");
    return JSON.parse(decoder.decode(asset.bytes));
}

export async function runConsoleEntry(compilerBytes, selection) {
    const config = readManifest(selection.config);
    const deps = readManifest(selection.deps);
    const target = config.runtimeOptions?.tfm;
    const runtimeTarget = deps.runtimeTarget?.name;
    if (typeof target !== "string" || typeof runtimeTarget !== "string")
        throw Error("The original runtime configuration and dependency target are required.");

    const companions = readManagedCompanions(selection, deps, runtimeTarget);
    const compilerModule = await WebAssembly.compile(compilerBytes);
    if (WebAssembly.Module.imports(compilerModule).length)
        throw Error("Expected the standalone self-hosted StandardWasm compiler.");
    const { exports: compiler } = await WebAssembly.instantiate(compilerModule);
    function upload(bytes) {
        const source = compiler.CreateBytes(bytes.length);
        bytes.forEach((value, index) => compiler.SetByte(source, index, value));
        return source;
    }
    const capacity = 1 << 20;
    const output = compiler.CreateBytes(capacity);
    const entryToken = selection.entryToken;
    // The PE reader verified the MethodDef token. The compiler takes its row.
    const row = entryToken & 0xffffff;
    const result = { entry: selection.entry.path, entryToken, target, runtimeTarget };
    // Detailed compilation reports assembly, MethodDef row and IL offset,
    // for example for a Console member outside the host contract.
    const location = compiler.CreateDiagnostic();
    let length;
    if (companions.length === 0)
        length = compiler.CompileDetailed(upload(selection.entry.bytes), row, output, location);
    else {
        const [bundle, entries] = packEntry(selection.entry, companions, row);
        length = compiler.CompileBundleDetailed(upload(bundle), upload(entries), output, location);
    }
    if (length < 0) return {
        ...result, state: "unsupported", compilerStatus: length,
        diagnostic: [0, 1, 2].map(index => compiler.ReadDiagnostic(location, index))
    };
    if (!length || length > capacity) throw Error("The self-hosted compiler returned an invalid module length.");

    const code = Uint8Array.from({ length }, (_, i) => compiler.ReadByte(output, i));
    const module = await WebAssembly.compile(code);
    // Only the console contract is provided to generated code.
    const imports = WebAssembly.Module.imports(module);
    if (imports.some(entry => entry.module !== consoleImportModule || entry.kind !== "function"))
        throw Error("The generated module requests a runtime import outside the console host contract.");
    const host = createConsoleHost(selection.console);
    const { exports } = await WebAssembly.instantiate(module, imports.length ? host.imports : {});
    if (typeof exports.run !== "function" || exports.run.length > 1)
        throw Error("This entry host supports Main() and Main(string[]).");
    const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", code));
    const finished = {
        ...result, moduleBytes: length, moduleSha256: hex(digest), imports: imports.map(entry => entry.name),
        console: { encoding: host.encoding, newLine: host.newLine }
    };
    let returned;
    try {
        returned = exports.run.length ? exports.run(buildArguments(exports, selection.arguments ?? [])) : exports.run();
    } catch (error) {
        // An unhandled managed exception prints a CLR report and sets a Windows
        // exit status that this host does not reproduce, so it is not "finished".
        if (!(error instanceof WebAssembly.Exception)) throw error;
        return { ...finished, state: "unhandled-exception", ...streams(host) };
    }
    // A void Main exits with zero. Environment.ExitCode is not a supported member.
    const exitCode = returned === undefined ? 0 : returned;
    if (!Number.isInteger(exitCode)) throw Error("This entry host expects a scalar integer exit result.");
    return { ...finished, state: "finished", exitCode, ...streams(host) };
}

function streams(host) {
    const { stdout, stderr } = host.output();
    return { stdout: hex(stdout), stderr: hex(stderr) };
}
