import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";

// Skips `dotnet build`, `dotnet publish` and the build half of `dotnet run` when the
// sha256 of every input is unchanged since the last successful build with the same arguments.
//
// Inputs of a project are: every file under its folder (bin/, obj/, node_modules/, artifacts/
// and dot-folders excluded), the same for each ProjectReference (transitively), any file or
// folder a Compile/None/Content Include names outside the folder, Directory.Build.* and
// global.json/NuGet.config on the way up to the repository root, the dotnet SDK version and
// the command's arguments.
//
// The stamp lives inside the output folder (bin/<Configuration>/ or the publish -o folder),
// so deleting the outputs also deletes the stamp and forces a build.
//
// Never cached (always built as before): projects that copy another build's bin/ outputs
// through $(MSBuildThisFileDirectory) or $(OutDir), or that use <Import>, because an input
// hash cannot see those dependencies. STANDARDWASM_REBUILD=1 turns the cache off.

const excludedFolders = new Set(["bin", "obj", "node_modules", "artifacts", "TestResults"]);
const ancestorInputs = ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
    "global.json", "NuGet.config", "nuget.config"];
const valueOptions = new Set(["-c", "--configuration", "-o", "--output", "-f", "--framework",
    "-r", "--runtime", "--project", "-v", "--verbosity", "-a", "--arch", "--os"]);
let sdkVersion;

function sdk(root) {
    if (sdkVersion === undefined) {
        const result = spawnSync("dotnet", ["--version"], { cwd: root, encoding: "utf8" });
        sdkVersion = result.status === 0 ? result.stdout.trim() : "";
    }
    return sdkVersion;
}

// Returns the .csproj path for a project argument (a folder holding one project, or the file).
function projectFile(root, argument) {
    const path = resolve(root, argument);
    if (existsSync(path) && statSync(path).isFile()) return /\.(cs|fs|vb)proj$/.test(path) ? path : undefined;
    if (!existsSync(path)) return undefined;
    const projects = readdirSync(path).filter(name => /\.(cs|fs|vb)proj$/.test(name));
    return projects.length === 1 ? join(path, projects[0]) : undefined;
}

function option(args, ...names) {
    for (let i = 0; i < args.length - 1; i++) if (names.includes(args[i])) return args[i + 1];
    return undefined;
}

function projectArgument(args) {
    const named = option(args, "--project");
    if (named) return named;
    for (let i = 1; i < args.length; i++) {
        if (args[i] === "--") break;
        if (valueOptions.has(args[i])) { i++; continue; }
        if (!args[i].startsWith("-")) return args[i];
    }
    return undefined;
}

function addFolder(files, folder) {
    if (!existsSync(folder)) return;
    for (const entry of readdirSync(folder, { withFileTypes: true })) {
        if (entry.name.startsWith(".")) continue;
        const path = join(folder, entry.name);
        if (entry.isDirectory()) { if (!excludedFolders.has(entry.name)) addFolder(files, path); }
        else if (entry.isFile()) files.add(path);
    }
}

// Adds one project's inputs and recurses into its ProjectReferences. Returns false when the
// project has a dependency the hash cannot see.
function addProject(files, visited, project) {
    if (visited.has(project)) return true;
    visited.add(project);
    const text = readFileSync(project, "utf8");
    if (/\$\((MSBuildThisFileDirectory|OutDir|OutputPath)\)|<Import\s/.test(text)) return false;
    const folder = dirname(project);
    addFolder(files, folder);
    for (const [, kind, include] of text.matchAll(/<(ProjectReference|Compile|None|Content|EmbeddedResource)\s+Include="([^"]+)"/g)) {
        for (const item of include.split(";")) {
            const path = resolve(folder, item.trim().replace(/\\/g, "/"));
            if (kind === "ProjectReference") {
                if (!existsSync(path) || !addProject(files, visited, path)) return false;
                continue;
            }
            // A wildcard names a folder; hash all of it (a superset of what MSBuild reads).
            const wildcard = path.search(/[*?]/);
            if (wildcard >= 0) addFolder(files, dirname(path.slice(0, wildcard) + "x"));
            else if (existsSync(path) && statSync(path).isFile()) files.add(path);
            else if (existsSync(path)) addFolder(files, path);
        }
    }
    for (const [, hint] of text.matchAll(/<HintPath>([^<]+)<\/HintPath>/g)) {
        const path = resolve(folder, hint.trim().replace(/\\/g, "/"));
        if (!existsSync(path)) return false;
        files.add(path);
    }
    for (let up = folder; ; up = dirname(up)) {
        for (const name of ancestorInputs) if (existsSync(join(up, name))) files.add(join(up, name));
        if (dirname(up) === up) break;
    }
    return true;
}

function inputHash(root, project, args) {
    const files = new Set();
    if (!addProject(files, new Set(), project)) return undefined;
    const hash = createHash("sha256");
    hash.update(JSON.stringify({ sdk: sdk(root), args }));
    for (const file of [...files].sort()) {
        hash.update("\0" + relative(root, file).replace(/\\/g, "/") + "\0");
        hash.update(readFileSync(file));
    }
    return hash.digest("hex");
}

// One stamp per output folder. The hash covers the arguments, so a build with other arguments
// into the same folder replaces the stamp and the first argument set rebuilds next time.
function stampPath(project, args) {
    const output = option(args, "-o", "--output");
    const configuration = option(args, "-c", "--configuration") ?? "Debug";
    const folder = output ? output : join(dirname(project), "bin", configuration);
    return join(folder, ".standardwasm-inputs.sha256");
}

// Plans a dotnet command. Returns { skip, args, record } where `args` is what to run (undefined
// when skipped) and record() writes the stamp after a successful run.
export function planDotnet(root, args) {
    const none = { skip: false, args, record() {} };
    if (process.env.STANDARDWASM_REBUILD === "1") return none;
    const verb = args[0];
    if (!["build", "publish", "run"].includes(verb) || args.includes("--no-build")) return none;
    const argument = projectArgument(args);
    const project = argument && projectFile(root, argument);
    if (!project) return none;
    // The run prebuild keeps only the configuration; options that pick another output folder
    // would make `run --no-build` read a different build, so such runs are not cached.
    if (verb === "run") {
        const own = args.indexOf("--") >= 0 ? args.slice(0, args.indexOf("--")) : args;
        if (own.some(arg => ["-a", "--arch", "-r", "--runtime", "-f", "--framework", "--os", "-o", "--output"].includes(arg)
            || /^(-p|--property)[:=]/i.test(arg))) return none;
    }
    // `dotnet run` is split into a cached build and `run --no-build` with the same configuration.
    const buildArgs = verb === "run"
        ? ["build", "-c", option(args, "-c", "--configuration") ?? "Debug", relative(root, project)]
        : args;
    const hash = inputHash(root, project, buildArgs);
    if (!hash) return none;
    const stamp = resolve(root, stampPath(project, buildArgs));
    const current = existsSync(stamp) && readFileSync(stamp, "utf8") === hash;
    // A build that starts invalidates the stamp first, so a failed or interrupted build that
    // already replaced some outputs can never be mistaken for a current one.
    if (!current) rmSync(stamp, { force: true });
    const record = () => { mkdirSync(dirname(stamp), { recursive: true }); writeFileSync(stamp, hash); };
    const label = relative(root, project).replace(/\\/g, "/");
    if (verb === "run") {
        const runArgs = [...args.slice(0, 1), "--no-build", ...args.slice(1)];
        return current
            ? { skip: false, args: runArgs, note: `dotnet build skipped (inputs unchanged): ${label}`, record() {} }
            : { skip: false, args: runArgs, prebuild: buildArgs, record };
    }
    return current
        ? { skip: true, note: `dotnet ${verb} skipped (inputs unchanged): ${label}`, record() {} }
        : { skip: false, args, record };
}
