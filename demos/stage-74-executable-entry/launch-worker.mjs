import { runManagedEntry } from "./launch.mjs";

self.addEventListener("message", async ({ data }) => {
    try {
        const response = await fetch("/StandardWasm.Compiler.wasm");
        if (!response.ok) throw Error("The self-hosted compiler is unavailable.");
        self.postMessage({ result: await runManagedEntry(await response.arrayBuffer(), data) });
    } catch (error) {
        self.postMessage({ error: error.message });
    }
});
