export const comDispatchSignature = "stdcall:i32(p32)";

export const comCookieLookupSignature = "stdcall:i32(p32,u32,p32,p32)";

export const comActivationName = "coCreateInstance";

export function isComDispatchSignature(signature) {
    return signature === comDispatchSignature || signature === comCookieLookupSignature;
}

const pageSize = 65536;
const wordSize = 4;
const maxToken = 0x7fffffff;
const guidSize = 16;
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const invalidPointer = 0x80004003 | 0;
const invalidArgument = 0x80070057 | 0;
const noInterface = 0x80004002 | 0;
const noAggregation = 0x80040110 | 0;
const classNotRegistered = 0x80040154 | 0;
let nextToken = 1;
const dispatchTypes = new Map();

// Re-export the JS callback as a Wasm function so the engine checks the
// guest import against the exact native-word arity at instantiation.
function typedDispatch(dispatch, parameters) {
    let dispatchType = dispatchTypes.get(parameters);
    if (!dispatchType) {
        dispatchType = new WebAssembly.Module(Uint8Array.of(
            0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00,
            1, parameters + 5, 1, 0x60, parameters, ...Array(parameters).fill(0x7f), 1, 0x7f,
            2, 5, 1, 0, 0, 0, 0, // import ""."" with type 0
            7, 8, 1, 4, 0x63, 0x61, 0x6c, 0x6c, 0, 0 // export "call"
        ));
        dispatchTypes.set(parameters, dispatchType);
    }
    return new WebAssembly.Instance(dispatchType, { "": { "": dispatch } }).exports.call;
}

export function createComHost(memory) {
    const receivers = new Map();
    const targets = new Map();
    const classes = new Map();
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
                !isComDispatchSignature(method?.signature) || typeof method.invoke !== "function")
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

    function registerClass(classId, interfaces, contexts = 1) {
        if (typeof classId !== "string" || !guidPattern.test(classId) || !(interfaces instanceof Map) ||
            interfaces.size === 0 || !Number.isInteger(contexts) || contexts <= 0 || contexts > 0xffffffff)
            throw new TypeError("StandardWasm: invalid COM class registration");
        const factories = new Map();
        for (const [interfaceId, factory] of interfaces) {
            if (typeof interfaceId !== "string" || !guidPattern.test(interfaceId) ||
                typeof factory !== "function" || factories.has(interfaceId.toLowerCase()))
                throw new TypeError("StandardWasm: invalid COM class interface");
            factories.set(interfaceId.toLowerCase(), factory);
        }
        const identity = classId.toLowerCase();
        if (classes.has(identity)) throw new TypeError("StandardWasm: duplicate COM class registration");
        classes.set(identity, { contexts, factories });
    }

    function activate(classPointer, outer, context, interfacePointer, resultPointer) {
        const words = new DataView(memory.buffer);
        const output = resultPointer >>> 0;
        if (!output) return invalidArgument;
        if (output > words.byteLength - wordSize) return invalidPointer;
        const classId = readGuid(words, classPointer >>> 0);
        const interfaceId = readGuid(words, interfacePointer >>> 0);
        words.setUint32(output, 0, true);
        if (!classId || !interfaceId) return invalidPointer;
        const registration = classes.get(classId);
        if (!registration || !(registration.contexts & context)) return classNotRegistered;
        if (outer) return noAggregation;
        const factory = registration.factories.get(interfaceId);
        if (!factory) return noInterface;
        const receiver = registerInterface(interfaceId, factory());
        // The registry can grow the shared memory and detach the previous view.
        new DataView(memory.buffer).setUint32(output, receiver, true);
        return 0;
    }

    function dispatch(signature, token, pointer, ...arguments_) {
        const receiver = pointer >>> 0;
        const binding = receivers.get(receiver);
        const target = targets.get(token);
        if (!binding || !target || target.receiver !== receiver || target.interfaceId !== binding.interfaceId ||
            target.signature !== signature || binding.slots.get(target.slot) !== token)
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

        const result = target.invoke(receiver, ...arguments_);
        if (!Number.isInteger(result) || result < -0x80000000 || result > 0xffffffff)
            throw new TypeError("StandardWasm: COM callback must return an i32");
        return result | 0;
    }

    return {
        imports: {
            [comDispatchSignature]: typedDispatch((token, receiver) => dispatch(comDispatchSignature, token, receiver), 2),
            [comCookieLookupSignature]: typedDispatch((token, receiver, cookie, iid, result) =>
                dispatch(comCookieLookupSignature, token, receiver, cookie >>> 0, iid >>> 0, result >>> 0), 5),
            [comActivationName]: typedDispatch(activate, 5)
        },
        registerInterface,
        registerClass
    };
}

function readGuid(view, address) {
    if (!address || address > view.byteLength - guidSize) return null;
    // Windows GUID stores Data1 through Data3 little-endian and Data4 as bytes.
    const first = view.getUint32(address, true).toString(16).padStart(8, "0");
    const second = view.getUint16(address + 4, true).toString(16).padStart(4, "0");
    const third = view.getUint16(address + 6, true).toString(16).padStart(4, "0");
    let tail = "";
    for (let index = 8; index < guidSize; index++)
        tail += view.getUint8(address + index).toString(16).padStart(2, "0");
    return `${first}-${second}-${third}-${tail.slice(0, 4)}-${tail.slice(4)}`;
}
