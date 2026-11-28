import { packEntry, readManagedCompanions } from "../../stage-74-executable-entry/companions.mjs";
import { frameworkInventory, frameworkMissingExitCode, frameworkMissingMessage, resolveFrameworks } from "../../stage-74-executable-entry/frameworks.mjs";
import { instantiateManagedModule } from "../../stage-74-executable-entry/resource-host.mjs";
import { buildArguments, createConsoleHost, hex } from "./console-host.mjs";

// Compiles a dropped .NET entry with the self-hosted compiler and runs it
// with the console host of decision 0039. The compile half follows the
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
    // Issue #299: as hostfxr, resolve the shared frameworks before anything is compiled. The
    // missing-framework report goes to stderr in the console encoding, and stdout stays empty.
    const frameworks = resolveFrameworks(config, frameworkInventory(selection));
    if (!frameworks.ok) {
        const host = createConsoleHost(selection.console);
        const text = frameworkMissingMessage(frameworks, selection.entry.path);
        for (let i = 0; i < text.length; i++) host.imports["standardwasm:console"].write(2, text.charCodeAt(i));
        const { stdout, stderr } = host.output();
        return { entry: selection.entry.path, entryToken: selection.entryToken, target, runtimeTarget,
            state: "framework-missing", exitCode: frameworkMissingExitCode, missingFramework: frameworks.missing,
            stdout: hex(stdout), stderr: hex(stderr) };
    }

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
    const origins = compiler.BundleModuleOrigins?.() === 1;
    let length;
    if (companions.length === 0 && !origins)
        length = compiler.CompileDetailed(upload(selection.entry.bytes), row, output, location);
    else {
        const [bundle, entries] = packEntry(selection.entry, companions, row, origins);
        length = compiler.CompileBundleDetailed(upload(bundle), upload(entries), output, location);
    }
    if (length < 0) return {
        ...result, state: "unsupported", compilerStatus: length,
        diagnostic: [0, 1, 2].map(index => compiler.ReadDiagnostic(location, index))
    };
    if (!length || length > capacity) throw Error("The self-hosted compiler returned an invalid module length.");

    const code = Uint8Array.from({ length }, (_, i) => compiler.ReadByte(output, i));
    const module = await WebAssembly.compile(code);
    const imports = WebAssembly.Module.imports(module);
    const host = createConsoleHost(selection.console);
    const instance = await instantiateManagedModule(module, compiler, [selection.entry, ...companions],
        host.imports, selection.executablePath);
    const { exports } = instance;
    if (typeof exports.run !== "function" || exports.run.length > 1)
        throw Error("This entry host supports Main() and Main(string[]).");
    const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", code));
    const finished = {
        ...result, moduleBytes: length, moduleSha256: hex(digest), imports: imports.map(entry => entry.name),
        console: { encoding: host.encoding, newLine: host.newLine }
    };
    // A module that reads stdin calls a suspending import, so its entry runs through
    // WebAssembly.promising and returns a Promise (decision 0079). Others stay synchronous.
    const reads = imports.some(entry => entry.module === "standardwasm:console" && entry.name === "readLine");
    if (reads && !selection.console?.input) throw Error("This entry reads standard input; provide console.input.");
    const run = reads ? (instance.promising?.("run") ?? WebAssembly.promising(exports.run)) : exports.run;
    let returned;
    try {
        returned = exports.run.length ? run(buildArguments(exports, selection.arguments ?? [])) : run();
        if (reads) returned = await returned;
    } catch (error) {
        // Environment.Exit recorded a status and trapped (decision 0104).
        if (host.exitStatus() !== null) return { ...finished, state: "finished", exitCode: host.exitStatus(), ...streams(host) };
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
