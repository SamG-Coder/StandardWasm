import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { root, run } from "../../scripts/toolchain.mjs";

if (process.platform !== "win32") throw Error("The Windows reference requires Windows.");
const stage = "demos/stage-72-winforms-windows-reference";
const evidenceRoot = join(root, "artifacts", "winforms-windows-reference");
mkdirSync(evidenceRoot, { recursive: true });
const output = mkdtempSync(join(evidenceRoot, "run-"));
const ordinary = join(output, "ordinary");
const single = join(output, "single");
console.log("Windows reference evidence at " + output);
const sdk = spawnSync("dotnet", ["--version"], { encoding: "utf8", cwd: root });
if (sdk.status !== 0) throw Error("Could not detect the .NET SDK", { cause: sdk.error });
writeFileSync(join(output, "sdk.txt"), sdk.stdout);
const project = stage + "/App/WinFormsReference.csproj";
run("dotnet", ["publish", project, "-c", "Release", "--no-self-contained", "-o", ordinary]);
run("dotnet", ["publish", project, "-c", "Release", "--no-self-contained",
    "-r", "win-x64", "-p:PublishSingleFile=true", "-p:DebugType=embedded", "-o", single]);
run("dotnet", ["run", "-c", "Release", "--project", stage + "/Verify", "--", ordinary, single, output]);
