import { join } from "node:path";
import { root } from "../../../scripts/toolchain.mjs";
import { serveIntake } from "../../stage-73-executable-intake/server.mjs";
import { launchAssets } from "../../stage-74-executable-entry/launch-server.mjs";

// Serves the Stage 110 page over the unchanged Stage 73 intake server and
// loaders. The import paths mirror the source tree so modules resolve as-is.
export function serveConsoleLaunch(port, compiler) {
    const demos = join(root, "demos");
    const script = path => ["/" + path, ["text/javascript", join(demos, path)]];
    return serveIntake(port, new Map([
        ["/", ["text/html", join(demos, "stage-110-console-arguments/web/index.html")]],
        script("stage-110-console-arguments/web/console-ui.mjs"),
        ...["console-worker", "console-launch", "console-host"].map(name =>
            script("stage-110-console-arguments/host/" + name + ".mjs")),
        ...launchAssets(compiler, "/stage-74-executable-entry")
    ]));
}
