export const comDispatchSignature = "stdcall:i32(p32)";

const pageSize = 65536;
const wordSize = 4;
const maxToken = 0x7fffffff;
let nextToken = 1;
let dispatchType;

// Re-export the JS callback as a Wasm function so the engine checks the
// guest import against (i32 token, i32 receiver) -> i32 at instantiation.
function typedDispatch(dispatch) {
    dispatchType ??= new WebAssembly.Module(Uint8Array.of(
        0x00, 0x61, 0x73, 0x6d, 0x01, 0, 0, 0,
        1, 7, 1, 0x60, 2, 0x7f, 0x7f, 1, 0x7f, // function type
        2, 5, 1, 0, 0, 0, 0, // import ""."" with type 0
        7, 8, 1, 4, 0x63, 0x61, 0x6c, 0x6c, 0, 0 // export "call"
    ));
    return new WebAssembly.Instance(dispatchType, { "": { "": dispatch } }).exports.call;
}

export function createComHost(memory) {
    const receivers = new Map();
    const targets = new Map();
    let nextAddress = 0;
    let limit = 0;

    function allocate(bytes) {
        if (bytes > limit - nextAddress) {
            const reserveNull = memory.buffer.byteLength === 0 ? wordSize : 0;
            const pages = Math.ceil((bytes + reserveNull) / pageSize);
            // Reuse only pages allocated by this registry. Wasm and other
            // platform adapters can independently grow the same memory.
            nextAddress = memory.grow(pages) * pageSize;
            limit = nextAddress + pages * pageSize;
            nextAddress += reserveNull;
        }
        const address = nextAddress;
        nextAddress += bytes;
        return address;
    }

    function registerInterface(interfaceId, slots) {
        if (typeof interfaceId !== "string" || !interfaceId || interfaceId.includes("\0") ||
            !(slots instanceof Map) || slots.size === 0)
            throw new TypeError("StandardWasm: invalid COM interface registration");
        if (slots.size > maxToken - nextToken + 1)
            throw new RangeError("StandardWasm: exhausted COM call tokens");

        const methods = new Map();
        let length = 0;
        for (const [slot, method] of slots) {
            if (!Number.isInteger(slot) || slot < 0 || slot >= (0x100000000 / wordSize) - 1 ||
                method?.signature !== comDispatchSignature || typeof method.invoke !== "function")
                throw new TypeError("StandardWasm: invalid COM slot registration");
            methods.set(slot, { signature: method.signature, invoke: method.invoke });
            length = Math.max(length, slot + 1);
        }

        const receiver = allocate(wordSize * (1 + length));
        const vtable = receiver + wordSize;
        // The registration identity includes the particular interface pointer.
        // Several objects may implement the same interface id.
        const binding = { interfaceId, vtable, length, slots: new Map() };
        const words = new DataView(memory.buffer);
        words.setUint32(receiver, vtable, true);
        for (const [slot, { signature, invoke }] of methods) {
            // Never reuse a token between live registries in the same JS realm.
            const token = nextToken++;
            binding.slots.set(slot, token);
            targets.set(token, { receiver, interfaceId, slot, signature, invoke });
            words.setInt32(vtable + wordSize * slot, token, true);
        }
        receivers.set(receiver, binding);
        return receiver;
    }

    function dispatch(token, pointer) {
        const receiver = pointer >>> 0;
        const binding = receivers.get(receiver);
        const target = targets.get(token);
        if (!binding || !target || target.receiver !== receiver || target.interfaceId !== binding.interfaceId ||
            target.signature !== comDispatchSignature || binding.slots.get(target.slot) !== token)
            throw new Error("StandardWasm: unregistered COM receiver or call token");

        // A guest may copy a valid token to another slot. Validate the
        // registered vtable, including gaps, before trusting the token.
        const words = new DataView(memory.buffer);
        if (words.getUint32(receiver, true) !== binding.vtable)
            throw new Error("StandardWasm: COM receiver or vtable slot was modified");
        for (let slot = 0; slot < binding.length; slot++) {
            if (words.getInt32(binding.vtable + wordSize * slot, true) !== (binding.slots.get(slot) ?? 0))
                throw new Error("StandardWasm: COM receiver or vtable slot was modified");
        }

        const result = target.invoke(receiver);
        if (!Number.isInteger(result) || result < -0x80000000 || result > 0xffffffff)
            throw new TypeError("StandardWasm: COM callback must return an i32");
        return result | 0;
    }

    return {
        imports: { [comDispatchSignature]: typedDispatch(dispatch) },
        registerInterface
    };
}
