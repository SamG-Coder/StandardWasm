import { join } from "node:path";
import { root } from "../../scripts/toolchain.mjs";
import { serveIntake } from "../stage-73-executable-intake/server.mjs";

export function launchAssets(compiler, route) {
    const stage = join(root, "demos/stage-74-executable-entry");
    // Both launcher workers load the same runtime with relative static imports.
    return [
        ["/StandardWasm.Compiler.wasm", ["application/wasm", compiler]],
        ["/scripts/native-imports.mjs", ["text/javascript", join(root, "scripts/native-imports.mjs")]],
        ["/stage-73-executable-intake/loader/pe.mjs", ["text/javascript",
            join(root, "demos/stage-73-executable-intake/loader/pe.mjs")]],
        ...["launch", "launch-worker", "companions", "resource-host", "fatal-host", "com-host",
            "dynamic-library-host", "temp-path-host", "full-path-host", "long-path-host", "win32-path-input",
            "frameworks", "win32-host", "activation-context", "dpi-awareness", "display-context", "windows-version",
            "window-state", "window-rect", "window-messages", "window-events", "window-send", "window-paint",
            "thread-handle-host", "window-station-host"].map(name => [
            route + "/" + name + ".mjs", ["text/javascript", join(stage, name + ".mjs")]
        ])
    ];
}

export function serveLaunch(port, compiler) {
    return serveIntake(port, new Map(launchAssets(compiler, "/launch")));
}
