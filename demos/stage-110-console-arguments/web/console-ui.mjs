import { openExecutable } from "/loader/intake.mjs";

// Drop-in page for Stage 110: select an original publish, give Main its
// arguments and the console configuration, and run it in a worker.
const status = document.querySelector("#status");
const run = document.querySelector("#run");
let selection, revision = 0, active;

// Main(string[]) receives exactly these strings, including empty ones.
function processOptions() {
    const values = JSON.parse(document.querySelector("#arguments").value);
    if (!Array.isArray(values) || values.some(value => typeof value !== "string"))
        throw Error("Arguments must be a JSON array of strings.");
    return {
        arguments: values,
        console: {
            encoding: document.querySelector("#encoding").value,
            newLine: document.querySelector("#newline").value === "lf" ? "\n" : "\r\n"
        }
    };
}

function show(state, text) {
    status.dataset.state = state;
    status.textContent = text;
}

for (const name of ["files", "folder"])
    document.querySelector("#" + name).addEventListener("change", event => inspect([...event.target.files]));

async function inspect(sources) {
    const current = ++revision;
    active?.();
    selection = undefined;
    run.disabled = true;
    show("loading", "Reading local files…");
    try {
        const files = await Promise.all(sources.map(async file => ({
            path: file.webkitRelativePath ? file.webkitRelativePath.split("/").slice(1).join("/") : file.name,
            bytes: new Uint8Array(await file.arrayBuffer())
        })));
        const executables = files.filter(file => /\.exe$/i.test(file.path));
        const candidates = executables.length ? executables : files.filter(file => /\.dll$/i.test(file.path));
        if (candidates.length !== 1) throw Error("Provide one executable and its original companions.");
        const opened = openExecutable(files, candidates[0].path);
        if (current !== revision) return;
        selection = opened;
        run.disabled = false;
        show("ready", JSON.stringify({ packageType: opened.packageType, entry: opened.entry.path }, null, 2));
    } catch (error) {
        if (current === revision) show("error", error.message);
    }
}

run.addEventListener("click", () => {
    if (!selection) return;
    let options;
    try { options = processOptions(); }
    catch (error) { show("error", error.message); return; }
    const current = revision;
    const worker = new Worker("/stage-110-console-arguments/host/console-worker.mjs", { type: "module" });
    run.disabled = true;
    show("running", "Compiling and running locally…");
    const deadline = setTimeout(() => finish("error", "The local entry did not finish within 15 seconds."), 15000);
    const cancel = () => {
        clearTimeout(deadline);
        worker.terminate();
        active = undefined;
    };
    active = cancel;
    function finish(state, text) {
        if (current !== revision || active !== cancel) return;
        cancel();
        show(state, text);
        run.disabled = false;
    }
    worker.onmessage = ({ data }) => data.error
        ? finish("error", data.error)
        : finish(data.result.state, JSON.stringify(data.result, null, 2));
    worker.onerror = () => finish("error", "The local launch worker failed.");
    worker.postMessage({ ...selection, ...options });
});
