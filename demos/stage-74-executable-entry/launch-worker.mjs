import { runManagedEntry } from "./launch.mjs";

self.addEventListener("message", async ({ data }) => {
    try {
        const response = await fetch("/StandardWasm.Compiler.wasm");
        if (!response.ok) throw Error("The self-hosted compiler is unavailable.");
        // Workers do not expose the top-level window's display information.
        const { displayCapabilities, ...selection } = data;
        self.postMessage({ result: await runManagedEntry(await response.arrayBuffer(), selection, { displayCapabilities }) });
    } catch (error) {
        self.postMessage({ error: error.message });
    }
});
