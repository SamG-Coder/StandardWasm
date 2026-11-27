import { join } from "node:path";
import { root } from "../../../scripts/toolchain.mjs";
import { serveIntake } from "../../stage-73-executable-intake/server.mjs";

// Serves the Stage 151 page over the Stage 73 intake server, reusing the Stage 110 console
// launcher and host. The import paths mirror the source tree so modules resolve as-is.
export function serveInteractiveConsole(port, compiler) {
    const demos = join(root, "demos");
    const script = path => ["/" + path, ["text/javascript", join(demos, path)]];
    return serveIntake(port, new Map([
        ["/", ["text/html", join(demos, "stage-151-interactive-stdin/web/index.html")]],
        ["/StandardWasm.Compiler.wasm", ["application/wasm", compiler]],
        ["/scripts/native-imports.mjs", ["text/javascript", join(root, "scripts/native-imports.mjs")]],
        script("stage-151-interactive-stdin/web/stdin-ui.mjs"),
        script("stage-151-interactive-stdin/host/stdin-worker.mjs"),
        ...["console-launch", "console-host"].map(name => script("stage-110-console-arguments/host/" + name + ".mjs")),
        script("stage-74-executable-entry/companions.mjs"),
        script("stage-74-executable-entry/resource-host.mjs"),
        script("stage-74-executable-entry/com-host.mjs"),
        script("stage-74-executable-entry/dynamic-library-host.mjs"),
        script("stage-74-executable-entry/win32-host.mjs"),
        script("stage-74-executable-entry/full-path-host.mjs"),
        script("stage-74-executable-entry/window-state.mjs"),
        script("stage-74-executable-entry/window-messages.mjs"),
        script("stage-74-executable-entry/activation-context.mjs"),
        script("stage-74-executable-entry/dpi-awareness.mjs"),
        script("stage-74-executable-entry/frameworks.mjs"),
        script("stage-73-executable-intake/loader/pe.mjs")
    ]));
}
