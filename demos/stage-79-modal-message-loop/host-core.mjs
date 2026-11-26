// Stage 79 hosts. Both run app.wasm's unmodified dispatch logic. They differ only in how getMessage waits.
export const TRACE_NAMES = ["main-shown", "posted-1", "click-begin", "posted-after-click", "posted-during-click",
    "dialog-shown", "posted-in-dialog", "dialog-timer", "click-after-dialog", "closing", "run-returned"];
const msg = (kind, arg) => (kind << 16) | arg;

// Counts host turns (macrotasks, and animation frames where available) while the modal dialog is open, to show
// whether the browser keeps running its event loop during the nested modal loop.
function modalMonitor() {
    let open = false, tasks = 0, frames = 0, openedAt = 0, ms = 0, interval;
    const raf = globalThis.requestAnimationFrame;
    return {
        dialogShown() {
            open = true; openedAt = performance.now();
            interval = setInterval(() => { if (open) tasks++; }, 4);
            if (raf) { const tick = () => { if (!open) return; frames++; raf(tick); }; raf(tick); }
        },
        dialogClosed() { open = false; ms = performance.now() - openedAt; clearInterval(interval); },
        result: () => ({ hostTasksDuringModal: tasks, framesDuringModal: raf ? frames : null, modalMs: +ms.toFixed(1) })
    };
}

// J: JS Promise Integration on the current thread. getMessage returns a queued message synchronously, or a Promise
// when the queue is empty, which suspends the whole Wasm stack (including the nested modal frame) until a message arrives.
export async function runJspi(bytes, { timerMs, echoCount, gc }) {
    const queue = [];
    let waiter = null, onEcho = null;
    const trace = [], monitor = modalMonitor();
    const deliver = m => { if (waiter) { const w = waiter; waiter = null; w(m); } else queue.push(m); };
    const host = {
        getMessage: new WebAssembly.Suspending(() => queue.length ? queue.shift() : new Promise(r => { waiter = r; })),
        post: m => (m >>> 16) === 7 ? onEcho(m & 0xffff) : deliver(m),
        // Collect garbage while the click handler's frame is suspended, so that frame's GC references must be traced.
        setTimer: ms => setTimeout(() => { if (gc) { gc(); collectedWhileSuspended++; } deliver(msg(4, 0)); }, ms),
        trace: id => {
            trace.push(TRACE_NAMES[id]);
            if (id === 5) monitor.dialogShown();
            if (id === 7) monitor.dialogClosed();
        }
    };
    let collectedWhileSuspended = 0;
    const { instance } = await WebAssembly.instantiate(bytes, { host });
    await WebAssembly.promising(instance.exports.run)(timerMs);
    const stateCheck = instance.exports.stateCheck();

    // Latency: each reply schedules the next message as a separate task (MessageChannel), so every round trip
    // suspends and resumes the Wasm stack once.
    const channel = new MessageChannel();
    channel.port1.onmessage = ({ data }) => deliver(msg(7, data));
    onEcho = i => channel.port2.postMessage(i + 1);
    deliver(msg(7, 0));
    const start = performance.now();
    await WebAssembly.promising(instance.exports.echo)(echoCount);
    const usPerTrip = +((performance.now() - start) * 1000 / echoCount).toFixed(2);
    channel.port1.close();
    return { trace, ...monitor.result(), usPerTrip, stateCheck, collectedWhileSuspended };
}

// W: the app runs in a dedicated worker and blocks in Atomics.wait. Layout of the shared Int32Array:
// [0] inbox head, [1] inbox tail, [2..2+N) inbox ring; [OUT] outbox head, [OUT+1] outbox tail, ring after it.
export const RING = 1024, OUT = 2 + RING;
export function runWorker(bytes, { timerMs, echoCount }, workerUrl) {
    return new Promise((resolve, reject) => {
        const shared = new Int32Array(new SharedArrayBuffer(4 * (4 + 2 * RING)));
        const trace = [], monitor = modalMonitor();
        const worker = new Worker(workerUrl, { type: "module" });
        const enqueue = m => {
            const tail = Atomics.load(shared, 1);
            shared[2 + (tail % RING)] = m;
            Atomics.store(shared, 1, tail + 1);
            Atomics.notify(shared, 1);
        };
        worker.onerror = e => reject(Error(e.message));
        worker.onmessage = async ({ data }) => {
            if (data.trace !== undefined) {
                trace.push(TRACE_NAMES[data.trace]);
                if (data.trace === 5) monitor.dialogShown();
                if (data.trace === 7) monitor.dialogClosed();
            } else if (data.timer !== undefined) {
                setTimeout(() => enqueue(msg(4, 0)), data.timer);
            } else if (data.ranApp) {
                const stateCheck = data.stateCheck;
                // Latency: the main thread waits asynchronously for each reply and posts the next message.
                enqueue(msg(7, 0));
                const start = performance.now();
                for (let i = 0; i < echoCount; i++) {
                    let head = Atomics.load(shared, OUT);
                    while (Atomics.load(shared, OUT + 1) === head) await Atomics.waitAsync(shared, OUT + 1, head).value;
                    const reply = shared[OUT + 2 + (head % RING)] & 0xffff;
                    Atomics.store(shared, OUT, head + 1);
                    if (i + 1 < echoCount) enqueue(msg(7, reply + 1));
                }
                const usPerTrip = +((performance.now() - start) * 1000 / echoCount).toFixed(2);
                worker.terminate();
                resolve({ trace, ...monitor.result(), usPerTrip, stateCheck, collectedWhileSuspended: null });
            }
        };
        worker.postMessage({ bytes, shared, timerMs, echoCount });
    });
}
