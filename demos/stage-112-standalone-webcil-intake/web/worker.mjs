import { inspectStandaloneEntry, openStandalonePublish } from "../loader/publish.mjs";

self.onmessage = async ({ data }) => {
    try {
        const files = await Promise.all(data.map(async ({ path, file }) =>
            ({ path, bytes: new Uint8Array(await file.arrayBuffer()) })));
        const selection = await openStandalonePublish(files);
        const compiler = await fetch("../compiler.wasm");
        if (!compiler.ok) throw Error("The self-hosted compiler is unavailable.");
        self.postMessage(await inspectStandaloneEntry(await compiler.arrayBuffer(), selection));
    } catch (error) {
        self.postMessage({ error: error.message });
    }
};
