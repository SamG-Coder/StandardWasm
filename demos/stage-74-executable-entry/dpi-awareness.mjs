export const dpiImportNames = [
    "isValidDpiAwarenessContext", "setProcessDpiAwarenessContext", "setProcessDPIAware",
    "setProcessDpiAwareness", "getThreadDpiAwarenessContext", "areDpiAwarenessContextsEqual",
    "getAwarenessFromDpiAwarenessContext", "isProcessDPIAware", "getProcessDpiAwareness"
];

const unaware = -1;
const systemAware = -2;
const unawareGdiScaled = -5;
const invalidParameter = 87;
const accessDenied = 5;
const hresultInvalidParameter = 0x80070057 | 0;
const hresultAccessDenied = 0x80070005 | 0;
const isValid = value => value >= unawareGdiScaled && value <= unaware;

export function createDpiAwareness(setSystemError) {
    // One process and one managed thread per launcher instance. Opaque Win32
    // thread-context handles are represented by their equivalent constants.
    let processContext = 0;
    const current = () => processContext || unaware;

    function trySet(value) {
        if (!isValid(value)) return invalidParameter;
        if (processContext) return accessDenied;
        processContext = value;
        return 0;
    }

    function awareness(value) {
        if (!isValid(value)) return -1;
        if (value === unaware || value === unawareGdiScaled) return 0;
        return value === systemAware ? 1 : 2;
    }

    return {
        isValidDpiAwarenessContext: value => Number(isValid(value)),
        setProcessDpiAwarenessContext(value) {
            const error = trySet(value);
            if (error) setSystemError(error);
            return Number(!error);
        },
        setProcessDPIAware() {
            if (!processContext) processContext = systemAware;
            return 1;
        },
        setProcessDpiAwareness(value) {
            const error = trySet(value >= 0 && value <= 2 ? -1 - value : 0);
            if (!error) return 0;
            setSystemError(error);
            return error === accessDenied ? hresultAccessDenied : hresultInvalidParameter;
        },
        getThreadDpiAwarenessContext: current,
        areDpiAwarenessContextsEqual: (first, second) => Number(first === second && isValid(first)),
        getAwarenessFromDpiAwarenessContext: awareness,
        getProcessDpiAwareness(handle) {
            const valid = handle === 0 || handle === -1;
            if (!valid) setSystemError(invalidParameter);
            return valid ? [0, awareness(current())] : [hresultInvalidParameter, -1];
        },
        isProcessDPIAware: () => Number(current() !== unaware && current() !== unawareGdiScaled)
    };
}
