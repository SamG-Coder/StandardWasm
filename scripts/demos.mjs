import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { root } from "./toolchain.mjs";

export function listDemos() {
    const base = join(root, "demos");
    return readdirSync(base, { withFileTypes: true })
        .filter(entry => entry.isDirectory() && /^stage-\d+-/.test(entry.name))
        .sort((a, b) => a.name.localeCompare(b.name))
        .map(entry => {
            const dir = join(base, entry.name);
            const manifest = JSON.parse(readFileSync(join(dir, "demo.json"), "utf8"));
            return {
                ...manifest, stage: entry.name,
                project: join(dir, manifest.assembly + ".csproj"),
                dll: join(dir, "bin/Release/netstandard2.0", manifest.assembly + ".dll"),
                wasm: join(root, "artifacts", manifest.assembly + ".wasm")
            };
        });
}
