// W candidate, worker side. getMessage blocks in Atomics.wait on the shared inbox. Messages the app posts to itself
// go straight into its own inbox, as PostMessage to the calling thread's queue does. Echo replies go to the outbox.
import { OUT, RING } from "./host-core.mjs";

self.onmessage = async ({ data: { bytes, shared, timerMs, echoCount } }) => {
    const push = (base, m) => {
        const tail = Atomics.load(shared, base + 1);
        shared[base + 2 + (tail % RING)] = m;
        Atomics.store(shared, base + 1, tail + 1);
        Atomics.notify(shared, base + 1);
    };
    const host = {
        getMessage() {
            const head = Atomics.load(shared, 0);
            while (Atomics.load(shared, 1) === head) Atomics.wait(shared, 1, head);
            const m = shared[2 + (head % RING)];
            Atomics.store(shared, 0, head + 1);
            return m;
        },
        post: m => (m >>> 16) === 7 ? push(OUT, m) : push(0, m),
        setTimer: ms => self.postMessage({ timer: ms }),
        trace: id => self.postMessage({ trace: id })
    };
    const { instance } = await WebAssembly.instantiate(bytes, { host });
    instance.exports.run(timerMs);
    self.postMessage({ ranApp: true, stateCheck: instance.exports.stateCheck() });
    instance.exports.echo(echoCount);
};
