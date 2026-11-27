import { join } from "node:path";
import { root } from "../../scripts/toolchain.mjs";
import { serveIntake } from "../stage-73-executable-intake/server.mjs";

export function serveLaunch(port, compiler) {
    const stage = join(root, "demos/stage-74-executable-entry");
    return serveIntake(port, new Map([
        ["/StandardWasm.Compiler.wasm", ["application/wasm", compiler]],
        ["/stage-73-executable-intake/loader/pe.mjs", ["text/javascript",
            join(root, "demos/stage-73-executable-intake/loader/pe.mjs")]],
        ...["launch", "launch-worker", "companions", "resource-host", "frameworks"].map(name => [
            "/launch/" + name + ".mjs", ["text/javascript", join(stage, name + ".mjs")]
        ])
    ]));
}
