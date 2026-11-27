import { runConsoleEntry } from "/stage-110-console-arguments/host/console-launch.mjs";
import { createLineInput } from "/stage-110-console-arguments/host/console-host.mjs";

// Stage 151 (decision 0079). Runs an entry that may call Console.ReadLine. Console.ReadLine
// suspends the module (JS Promise Integration), so this worker's event loop stays free to
// receive the lines the page submits after the program has started.
//   page -> worker: { start: selection }, { input: text }, { eof: true }
//   worker -> page: { output: { stream, text } } as the program writes, then { result } or { error }
const input = createLineInput();
let pending = { 1: "", 2: "" }, scheduled = false;

// Code units are batched per task so a prompt arrives before the read that follows it.
function flush() {
    scheduled = false;
    for (const stream of [1, 2]) {
        if (!pending[stream]) continue;
        self.postMessage({ output: { stream, text: pending[stream] } });
        pending[stream] = "";
    }
}

function onWrite(stream, unit) {
    pending[stream] += String.fromCharCode(unit);
    if (!scheduled) {
        scheduled = true;
        queueMicrotask(flush);
    }
}

self.addEventListener("message", async ({ data }) => {
    if (data.input !== undefined) { input.push(data.input); return; }
    if (data.eof) { input.close(); return; }
    if (!data.start) return;
    try {
        const response = await fetch("/StandardWasm.Compiler.wasm");
        if (!response.ok) throw Error("The self-hosted compiler is unavailable.");
        const selection = data.start;
        const result = await runConsoleEntry(await response.arrayBuffer(),
            { ...selection, console: { ...selection.console, input, onWrite } });
        flush();
        self.postMessage({ result });
    } catch (error) {
        flush();
        self.postMessage({ error: error.message });
    }
});
