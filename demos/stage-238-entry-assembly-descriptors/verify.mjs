import { join } from "node:path";
import { verifyManagedDemo } from "../../scripts/managed-demo-parity.mjs";
import { root, run } from "../../scripts/toolchain.mjs";

const name = "stage-238-entry-assembly-descriptors";
const runtime = join(root, "demos", name, "BrowserRuntime");
if (!process.env.STANDARDWASM_MONO_CORELIB) {
    run("dotnet", ["publish", join(runtime, "BrowserRuntime.csproj"), "-c", "Release",
        "-o", join(root, "artifacts", name, "runtime"), "-v:q"]);
    process.env.STANDARDWASM_MONO_CORELIB = join(runtime, "obj/Release/net10.0/linked/System.Private.CoreLib.dll");
}
await verifyManagedDemo(name);
