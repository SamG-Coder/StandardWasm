const moduleResourceFlags = 0x80 | 0x08;
const actctxSize = 32;
const resourceNameOffset = 20;
const moduleOffset = 28;
const invalidParameter = 87;
const invalidHandle = 6;
const noAccess = 998;
const resourceNameNotFound = 1814;

export function createActivationContexts(memory, findManifest, setSystemError) {
    const contexts = new Map();
    const stack = [];
    let nextHandle = 1;
    let nextCookie = 1;

    function fail(error, result = 0) {
        setSystemError(error);
        return result;
    }

    function createActCtxW(pointer) {
        const address = pointer >>> 0;
        const buffer = memory.buffer;
        if (!address || address > buffer.byteLength - actctxSize) return fail(noAccess, -1);
        // ACTCTXW uses four-byte pointers in the current Wasm32 target.
        const request = new DataView(buffer, address, actctxSize);
        if (request.getUint32(0, true) !== actctxSize || request.getUint32(4, true) !== moduleResourceFlags)
            return fail(invalidParameter, -1);

        const id = request.getUint32(resourceNameOffset, true);
        if (!id || id > 0xffff) return fail(invalidParameter, -1);
        const manifest = findManifest(request.getUint32(moduleOffset, true), id);
        if (!manifest) return fail(resourceNameNotFound, -1);

        // Keep the original manifest for the browser's side-by-side resolver.
        // At this stage context creation validates resource presence and ABI only.
        const handle = nextHandle++;
        contexts.set(handle, { manifest, references: 1 });
        return handle;
    }

    function writeHandle(pointer, value) {
        const address = pointer >>> 0;
        const buffer = memory.buffer;
        if (!address || address > buffer.byteLength - 4) return false;
        new DataView(buffer).setUint32(address, value, true);
        return true;
    }

    function releaseActCtx(handle) {
        const context = contexts.get(handle >>> 0);
        if (context && --context.references === 0) contexts.delete(handle >>> 0);
    }

    function getCurrentActCtx(pointer) {
        const handle = stack.at(-1)?.handle ?? 0;
        if (!writeHandle(pointer, handle)) return fail(noAccess);
        if (handle) contexts.get(handle).references++;
        return 1;
    }

    function activateActCtx(handle, pointer) {
        handle >>>= 0;
        const context = contexts.get(handle);
        if (!context) return fail(invalidHandle);
        if (!writeHandle(pointer, nextCookie)) return fail(noAccess);
        context.references++;
        stack.push({ handle, cookie: nextCookie++ });
        return 1;
    }

    function deactivateActCtx(flags, cookie) {
        if (flags !== 0) return fail(invalidParameter);
        const current = stack.at(-1);
        if (current?.cookie !== (cookie >>> 0)) return fail(invalidParameter);
        stack.pop();
        releaseActCtx(current.handle);
        return 1;
    }

    return { createActCtxW, getCurrentActCtx, activateActCtx, deactivateActCtx, releaseActCtx };
}
