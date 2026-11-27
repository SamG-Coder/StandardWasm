import { openExecutable } from "/loader/intake.mjs";

// Stage 151 page: run an original console publish in a worker, show stdout and stderr as
// they are written, and send lines typed after the program starts. "End of input" closes
// stdin, which Console.ReadLine reports as null. A main-thread timer shows the page stays
// responsive while a read waits.
const status = document.querySelector("#status");
const run = document.querySelector("#run");
const stdout = document.querySelector("#stdout"), stderr = document.querySelector("#stderr");
const input = document.querySelector("#input"), send = document.querySelector("#send"), eof = document.querySelector("#eof");
const ticks = document.querySelector("#ticks");
let selection, worker;
const transcript = [];
setInterval(() => { ticks.value = String(Number(ticks.value) + 1); }, 20);

function show(state, text) {
    status.dataset.state = state;
    status.textContent = text;
}

function setInputEnabled(enabled) {
    input.disabled = send.disabled = eof.disabled = !enabled;
}

for (const name of ["files", "folder"])
    document.querySelector("#" + name).addEventListener("change", event => inspect([...event.target.files]));

async function inspect(sources) {
    worker?.terminate();
    worker = undefined;
    selection = undefined;
    run.disabled = true;
    setInputEnabled(false);
    show("loading", "Reading local files…");
    try {
        const files = await Promise.all(sources.map(async file => ({
            path: file.webkitRelativePath ? file.webkitRelativePath.split("/").slice(1).join("/") : file.name,
            bytes: new Uint8Array(await file.arrayBuffer())
        })));
        const executables = files.filter(file => /\.exe$/i.test(file.path));
        const candidates = executables.length ? executables : files.filter(file => /\.dll$/i.test(file.path));
        if (candidates.length !== 1) throw Error("Provide one executable and its original companions.");
        selection = openExecutable(files, candidates[0].path);
        run.disabled = false;
        show("ready", JSON.stringify({ packageType: selection.packageType, entry: selection.entry.path }, null, 2));
    } catch (error) {
        show("error", error.message);
    }
}

run.addEventListener("click", () => {
    if (!selection) return;
    let values;
    try {
        values = JSON.parse(document.querySelector("#arguments").value);
        if (!Array.isArray(values) || values.some(value => typeof value !== "string"))
            throw Error("Arguments must be a JSON array of strings.");
    } catch (error) { show("error", error.message); return; }
    stdout.textContent = stderr.textContent = "";
    // The interleaved order of both streams as the worker delivered them (decision 0079).
    transcript.length = 0;
    worker?.terminate();
    worker = new Worker("/stage-151-interactive-stdin/host/stdin-worker.mjs", { type: "module" });
    run.disabled = true;
    setInputEnabled(true);
    show("running", "Running locally; type lines below.");
    const finish = (state, text) => {
        setInputEnabled(false);
        run.disabled = false;
        show(state, text);
    };
    worker.onmessage = ({ data }) => {
        if (data.output) {
            (data.output.stream === 1 ? stdout : stderr).textContent += data.output.text;
            const last = transcript[transcript.length - 1];
            if (last && last.stream === data.output.stream) last.text += data.output.text;
            else transcript.push({ stream: data.output.stream, text: data.output.text });
            document.querySelector("#transcript").textContent = JSON.stringify(transcript);
        }
        else if (data.error) finish("error", data.error);
        else finish(data.result.state, JSON.stringify(data.result, null, 2));
    };
    worker.onerror = () => finish("error", "The local launch worker failed.");
    worker.postMessage({ start: { ...selection, arguments: values, console: {
        encoding: document.querySelector("#encoding").value,
        newLine: document.querySelector("#newline").value === "lf" ? "\n" : "\r\n"
    } } });
});

document.querySelector("#input-form").addEventListener("submit", event => {
    event.preventDefault();
    if (!worker || input.disabled) return;
    // The terminator the console would receive from a Windows keyboard line.
    worker.postMessage({ input: input.value + "\r\n" });
    input.value = "";
});

eof.addEventListener("click", () => {
    if (!worker) return;
    worker.postMessage({ eof: true });
    setInputEnabled(false);
});
