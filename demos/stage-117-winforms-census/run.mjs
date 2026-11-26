import { spawnSync } from "node:child_process";
import { mkdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import assert from "node:assert/strict";
import { root, run } from "../../scripts/toolchain.mjs";

// Static WinForms blocker census. Host-side analysis only: it publishes the
// unchanged Stage 72 reference app, walks its call graph against the installed
// Windows Desktop and shared frameworks, and records the portable compiler's
// own verdict for every reached method. Nothing here runs in the browser.
if (process.platform !== "win32") throw Error("The WinForms census reads the installed Windows Desktop runtime.");
const stage = "demos/stage-117-winforms-census";
const version = process.env.STANDARDWASM_DESKTOP_VERSION ?? "10.0.10";
const output = join(root, "artifacts", "winforms-census");
const publish = join(output, "publish");
mkdirSync(output, { recursive: true });

// Decision 0044: CoreLib bodies are lowered only from a 4-byte native-width
// CoreLib, so the census reads the installed win-x86 packs. The WinForms
// assemblies are identical in the win-x86 and win-x64 Windows Desktop packs.
const x86Host = process.env.STANDARDWASM_X86_DOTNET || "C:/Program Files (x86)/dotnet/dotnet.exe";
const installed = spawnSync(x86Host, ["--list-runtimes"], { encoding: "utf8", windowsHide: true });
if (installed.error || installed.status !== 0) throw Error("Install the win-x86 .NET 10 runtimes or set STANDARDWASM_X86_DOTNET.");
const packs = new Map(installed.stdout.split(/\r?\n/).flatMap(line => {
    const match = /^([^ ]+) ([^ ]+) \[(.*)\]$/.exec(line);
    return match?.[2] === version ? [[match[1], join(match[3], version)]] : [];
}));
const shared = packs.get("Microsoft.NETCore.App");
const desktop = packs.get("Microsoft.WindowsDesktop.App");
if (!shared || !desktop) throw Error(`win-x86 Microsoft.NETCore.App and Microsoft.WindowsDesktop.App ${version} are required.`);

run("dotnet", ["publish", "demos/stage-72-winforms-windows-reference/App/WinFormsReference.csproj", "-c", "Release",
    "--no-self-contained", "-o", publish]);
run("dotnet", ["build", stage + "/Census", "-c", "Release"]);
const parallel = process.env.STANDARDWASM_CENSUS_PARALLEL ?? "12";
// STANDARDWASM_CENSUS_REUSE replays the verdicts of an earlier census.json
// for the same inputs, regenerating the rankings without recompiling.
const reuse = process.env.STANDARDWASM_CENSUS_REUSE ? ["--reuse", process.env.STANDARDWASM_CENSUS_REUSE] : [];
run("dotnet", [join(stage, "Census/bin/Release/net10.0/Census.dll"), join(publish, "WinFormsReference.dll"), desktop, shared,
    output, "--parallel", parallel, ...reuse]);

const census = JSON.parse(readFileSync(join(output, "census-summary.json"), "utf8"));
const reached = new Set(JSON.parse(readFileSync(join(output, "census.json"), "utf8")).methods.map(m => m.name));
for (const name of ["WinFormsReference.Program::Main", "WinFormsReference.ReferenceForm::.ctor",
    "System.Windows.Forms.Application::Run", "System.Windows.Forms.Application::.cctor"])
    assert.ok(reached.has(name), name + " is reached");
// The whole-program verdict from Program.Main is the boundary the live drop
// reports. It moves as the compiler grows, so only require a located verdict.
assert.match(census.compiler.entry.site, /^\[[^,]+, MethodDef \d+, IL /);
console.log(`Census: ${census.graph.reachedMethods} reached methods; entry stops at ${census.compiler.entry.site} ` +
    `(${census.compiler.entry.category}). Results in ${output}`);
