const currentProcess = -1, currentThread = -2;
const duplicateSameAccess = 2, stillActive = 259;
const invalidHandle = 6, notEnoughMemory = 8, notSupported = 50, invalidParameter = 87, noAccess = 998;

// Owned handle tokens are distinct across launches in the same JS realm.
let nextHandle = 0x20000000;
const lastHandle = 0x3fffffff;

export function createThreadHandleHost(memory, currentThreadId, setError) {
    const handles = new Map();
    const kindOf = handle => handle === currentProcess ? currentProcess :
        handle === currentThread ? currentThread : handles.get(handle);

    function fail(error) {
        setError(error);
        return 0;
    }

    function duplicateHandle(sourceProcess, source, targetProcess, targetPointer, _access, inherit, options) {
        const address = targetPointer >>> 0;
        const view = address === 0 ? undefined : new DataView(memory.buffer);
        if (view && address > view.byteLength - 4) return fail(noAccess);

        // Windows clears the output handle even when duplication fails.
        view?.setUint32(address, 0, true);
        if (targetProcess === 0) return fail(invalidParameter);
        const kind = kindOf(source);
        if (kindOf(sourceProcess) !== currentProcess || kindOf(targetProcess) !== currentProcess || kind === undefined)
            return fail(invalidHandle);
        if (inherit !== 0 || options !== duplicateSameAccess) return fail(notSupported);
        if (nextHandle > lastHandle) return fail(notEnoughMemory);
        const duplicate = nextHandle++;
        handles.set(duplicate, kind);
        view?.setUint32(address, duplicate, true);
        return 1;
    }

    function getExitCodeThread(handle, resultPointer) {
        if (kindOf(handle) !== currentThread) return fail(invalidHandle);
        const address = resultPointer >>> 0;
        const view = new DataView(memory.buffer);
        if (address === 0 || address > view.byteLength - 4) return fail(noAccess);
        // The host invokes this import synchronously on the launch's live GUI thread.
        view.setUint32(address, stillActive, true);
        return 1;
    }

    function closeHandle(handle) {
        if (handle === currentProcess || handle === currentThread || handles.delete(handle)) return 1;
        return fail(invalidHandle);
    }

    return {
        getCurrentProcess: () => currentProcess,
        getCurrentThread: () => currentThread,
        getCurrentThreadId: currentThreadId,
        duplicateHandle,
        getExitCodeThread,
        closeHandle
    };
}
