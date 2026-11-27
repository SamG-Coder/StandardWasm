import { join } from "node:path";
import { root } from "../../../scripts/toolchain.mjs";
import { serveIntake } from "../../stage-73-executable-intake/server.mjs";

// Serves the Stage 110 page over the unchanged Stage 73 intake server and
// loaders. The import paths mirror the source tree so modules resolve as-is.
export function serveConsoleLaunch(port, compiler) {
    const demos = join(root, "demos");
    const script = path => ["/" + path, ["text/javascript", join(demos, path)]];
    return serveIntake(port, new Map([
        ["/", ["text/html", join(demos, "stage-110-console-arguments/web/index.html")]],
        ["/StandardWasm.Compiler.wasm", ["application/wasm", compiler]],
        ["/scripts/native-imports.mjs", ["text/javascript", join(root, "scripts/native-imports.mjs")]],
        script("stage-110-console-arguments/web/console-ui.mjs"),
        ...["console-worker", "console-launch", "console-host"].map(name =>
            script("stage-110-console-arguments/host/" + name + ".mjs")),
        script("stage-74-executable-entry/companions.mjs"),
        script("stage-74-executable-entry/resource-host.mjs"),
        script("stage-74-executable-entry/win32-host.mjs"),
        script("stage-74-executable-entry/window-state.mjs"),
        script("stage-74-executable-entry/activation-context.mjs"),
        script("stage-74-executable-entry/dpi-awareness.mjs"),
        script("stage-74-executable-entry/frameworks.mjs"),
        script("stage-73-executable-intake/loader/pe.mjs")
    ]));
}
