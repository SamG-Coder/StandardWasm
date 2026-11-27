import { openExecutable } from "/loader/intake.mjs";

const status = document.querySelector("#status");
const drop = document.querySelector("#drop");
const staged = new Map();
let revision = 0;
const run = document.querySelector("#run");
run.hidden = !new URLSearchParams(location.search).has("run");
document.querySelector("#framework-option").hidden = run.hidden;
const framework = document.querySelector("#framework");
let selection, active;
framework.addEventListener("change", () => {
    const wasRunning = Boolean(active);
    active?.();
    run.disabled = !selection;
    if (wasRunning) {
        revision++;
        status.dataset.state = "ready";
        status.textContent = "Framework files changed. Ready to compile.";
    }
});
run.addEventListener("click", async () => {
    if (!selection) return;
    const current = revision;
    const worker = new Worker("/launch/launch-worker.mjs", { type: "module" });
    run.disabled = true;
    status.dataset.state = "running";
    status.textContent = "Compiling and running locally…";
    function finish(state, message) {
        if (current !== revision || active !== cancel) return;
        cancel();
        status.dataset.state = state;
        status.textContent = message;
        run.disabled = false;
    }
    const frameworkFiles = [...framework.files];
    const timeout = frameworkFiles.length ? 600 : 15;
    const deadline = setTimeout(() => finish("error", `The local entry did not finish within ${timeout} seconds.`), timeout * 1000);
    const cancel = () => {
        clearTimeout(deadline);
        worker.terminate();
        active = undefined;
    };
    active = cancel;
    worker.onmessage = ({ data }) => {
        if (current !== revision || active !== cancel) return;
        if (data.winEvent) {
            drop.dispatchEvent(new CustomEvent("standardwasm-winevent", { bubbles: true, detail: data.winEvent }));
            return;
        }
        if (data.error) finish("error", data.error);
        else finish(data.result.state, JSON.stringify(data.result, null, 2));
    };
    worker.onerror = () => finish("error", "The local launch worker failed.");
    try {
        const framework = await Promise.all(frameworkFiles.map(async file => ({
            path: file.name, bytes: new Uint8Array(await file.arrayBuffer())
        })));
        const { browserDisplayCapabilities } = await import("/launch/display-context.mjs");
        if (current === revision && active === cancel)
            worker.postMessage({ ...selection, framework, displayCapabilities: browserDisplayCapabilities() });
    } catch (error) {
        finish("error", error.message);
    }
});
drop.style.cssText = "border: 2px dashed; padding: 2rem; max-width: 28rem";
drop.addEventListener("dragover", event => event.preventDefault());
drop.addEventListener("drop", event => {
    event.preventDefault();
    inspect([...event.dataTransfer.files]);
});
for (const name of ["files", "folder"])
    document.querySelector("#" + name).addEventListener("change", event => inspect([...event.target.files]));

async function inspect(sources) {
    const current = ++revision;
    active?.();
    selection = undefined;
    run.disabled = true;
    // Dropping a new executable starts a fresh launch. A follow-up drop of
    // companion files completes the current launch without reselecting it.
    if (sources.some(file => /\.exe$/i.test(file.name) || file.webkitRelativePath)) staged.clear();
    for (const file of sources) {
        const path = file.webkitRelativePath ? file.webkitRelativePath.split("/").slice(1).join("/") : file.name;
        staged.set(path, file);
    }
    status.dataset.state = "loading";
    status.textContent = "Reading local files…";
    try {
        const files = await Promise.all([...staged].map(async ([path, file]) => ({
            path, bytes: new Uint8Array(await file.arrayBuffer())
        })));
        const executables = files.filter(file => /\.exe$/i.test(file.path));
        const candidates = executables.length ? executables : files.filter(file => /\.dll$/i.test(file.path));
        if (candidates.length !== 1) throw Error("Provide one executable and its original companions.");
        const result = openExecutable(files, candidates[0].path);
        const digest = async file => file ? Array.from(
            new Uint8Array(await crypto.subtle.digest("SHA-256", file.bytes)),
            byte => byte.toString(16).padStart(2, "0")).join("") : null;
        const summary = JSON.stringify({
            packageType: result.packageType,
            entry: result.entry.path,
            entryToken: result.entryToken,
            sha256: await digest(result.entry),
            depsSha256: await digest(result.deps),
            configSha256: await digest(result.config)
        }, null, 2);
        if (current !== revision) return;
        selection = result;
        run.disabled = false;
        status.textContent = summary;
        status.dataset.state = "ready";
    } catch (error) {
        if (current !== revision) return;
        status.textContent = error.message;
        status.dataset.state = "error";
    }
}
