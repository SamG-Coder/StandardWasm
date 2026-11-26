const folder = document.querySelector("#folder");
const drop = document.querySelector("#drop");
const status = document.querySelector("#status");
let active;

async function inspect(files) {
    active?.terminate();
    const worker = new Worker(new URL("./worker.mjs", import.meta.url), { type: "module" });
    active = worker;
    status.dataset.state = "working";
    status.textContent = "Reading the original publish.";
    worker.onmessage = ({ data }) => {
        if (worker !== active) return;
        status.dataset.state = data.error ? "error" : "complete";
        status.textContent = JSON.stringify(data, null, 2);
        worker.terminate();
        active = undefined;
    };
    worker.onerror = ({ message }) => worker.onmessage({ data: { error: message } });
    try {
        const selection = await files;
        if (worker === active) worker.postMessage(selection);
    } catch (error) {
        worker.onmessage({ data: { error: error.message } });
    }
}

async function readEntries(entry, path = "") {
    if (entry.isFile) {
        const file = await new Promise((resolve, reject) => entry.file(resolve, reject));
        return [{ path: path + file.name, file }];
    }
    const reader = entry.createReader();
    const files = [];
    for (;;) {
        const entries = await new Promise((resolve, reject) => reader.readEntries(resolve, reject));
        if (entries.length === 0) return files;
        for (const child of entries) files.push(...await readEntries(child, path + entry.name + "/"));
    }
}

folder.addEventListener("change", () => {
    inspect(Array.from(folder.files, file => ({
        path: file.webkitRelativePath.split("/").slice(1).join("/"), file
    })));
});
drop.addEventListener("dragover", event => event.preventDefault());
drop.addEventListener("drop", event => {
    event.preventDefault();
    const entries = [...event.dataTransfer.items].map(item => item.webkitGetAsEntry());
    if (entries.length !== 1 || !entries[0]?.isDirectory) {
        active?.terminate();
        active = undefined;
        status.dataset.state = "error";
        status.textContent = "Drop a single publish folder.";
        return;
    }
    inspect(readEntries(entries[0]).then(files => files.map(({ path, file }) => ({
        path: path.slice(path.indexOf("/") + 1), file
    }))));
});
