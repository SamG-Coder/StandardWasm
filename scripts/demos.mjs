import { readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { root } from "./toolchain.mjs";

export function listDemos() {
    const base = join(root, "demos");
    return readdirSync(base, { withFileTypes: true })
        .filter(entry => entry.isDirectory() && /^stage-\d+-/.test(entry.name))
        .sort((a, b) => a.name.localeCompare(b.name))
        .map(entry => {
            const dir = join(base, entry.name);
            const manifest = JSON.parse(readFileSync(join(dir, "demo.json"), "utf8"));
            if (manifest.runner) return { ...manifest, stage: entry.name };
            return {
                ...manifest, stage: entry.name,
                project: join(dir, manifest.assembly + ".csproj"),
                dll: join(dir, "bin/Release/netstandard2.0", manifest.assembly + ".dll"),
                wasm: join(root, "artifacts", manifest.assembly + ".wasm")
            };
        });
}

export function demoInputs(demo) {
    return demo.inputs.map(input => {
        if (input?.file === "bundle") {
            const images = [readFileSync(demo.dll), ...input.dependencies.map(name =>
                readFileSync(join(dirname(demo.dll), name)))];
            const parts = images.flatMap(bytes => {
                const header = Buffer.alloc(4);
                header.writeUInt32LE(bytes.length);
                return [header, bytes];
            });
            return { bytes: Array.from(Buffer.concat(parts)) };
        }
        if (input?.file === "self") return { bytes: Array.from(readFileSync(demo.dll)) };
        if (typeof input?.name === "string") return { name: Array.from(Buffer.from(input.name, "utf8")) };
        return input;
    });
}
