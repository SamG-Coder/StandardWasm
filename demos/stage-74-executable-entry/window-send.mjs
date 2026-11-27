const invalidWindow = 1400;
const noAccess = 998;
const notSupported = 50;
const broadcastWindow = 0xffff;

export function createWindowSender(memory, windows, setError) {
    return function sendMessageTimeoutW(window, message, wParam, lParam, _flags, _timeout, resultPointer) {
        const handle = window >>> 0;
        const address = resultPointer >>> 0;
        if (address && address > memory.buffer.byteLength - 4) {
            setError(noAccess);
            return 0;
        }
        if (handle === broadcastWindow || !windows.getWindowOwner(handle)) {
            if (address) new DataView(memory.buffer).setUint32(address, 0, true);
            setError(handle === broadcastWindow ? notSupported : invalidWindow);
            return 0;
        }

        // The current host places all of a launch's windows on one GUI queue.
        // Win32 invokes the procedure directly for that queue and ignores the deadline.
        const result = windows.dispatchWindowMessage(handle, message >>> 0, wParam >>> 0, lParam | 0);
        // A reentrant procedure may grow linear memory while it handles the message.
        if (address) new DataView(memory.buffer).setUint32(address, result >>> 0, true);
        return 1;
    };
}
