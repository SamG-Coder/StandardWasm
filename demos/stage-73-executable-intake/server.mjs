import { createServer } from "node:http";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { root } from "../../scripts/toolchain.mjs";

export async function serveIntake(port, extra = new Map()) {
    const pageRoot = join(root, "demos/stage-73-executable-intake");
    const files = new Map([
        ["/", ["text/html", join(pageRoot, "web/index.html")]],
        ...["web/intake-ui", "loader/intake", "loader/pe", "loader/bundle"].map(path =>
            ["/" + path + ".mjs", ["text/javascript", join(pageRoot, path + ".mjs")]]),
        ...extra
    ]);
    const server = createServer((request, response) => {
        const [type, path] = files.get(new URL(request.url, "http://127.0.0.1").pathname) ?? [];
        if (request.method !== "GET" || !path) { response.writeHead(404).end(); return; }
        try {
            const body = readFileSync(path);
            response.writeHead(200, { "Content-Type": type });
            response.end(body);
        } catch {
            response.writeHead(404).end();
        }
    });
    await new Promise((resolve, reject) => {
        server.once("error", reject);
        server.listen(port, "127.0.0.1", resolve);
    });
    return {
        url: "http://127.0.0.1:" + server.address().port,
        close: () => new Promise(resolve => server.close(resolve))
    };
}
