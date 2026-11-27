class ManagedFailFastError extends Error {
    constructor(message, source, exception) {
        const parts = ["Managed application terminated."];
        if (source !== null) parts.push(source);
        if (message !== null) parts.push(message);
        super(parts.join(" "));
        this.name = "ManagedFailFastError";
        this.managedMessage = message;
        this.errorSource = source;
        this.managedException = exception;
    }
}

export function createFatalHost() {
    let failure;
    const text = ["", ""];

    function guard(value, maySuspend = false) {
        return new Proxy(value, {
            apply(target, receiver, args) {
                if (failure) throw failure;
                // The Wasm trap bypasses managed catch/finally. At the outer
                // host boundary, expose the fatal context and retire the app.
                try {
                    const result = Reflect.apply(target, receiver, args);
                    return maySuspend ? result.then(value => {
                        if (failure) throw failure;
                        return value;
                    }, error => { throw failure ?? error; }) : result;
                } catch (error) {
                    throw failure ?? error;
                }
            }
        });
    }

    return {
        imports: {
            write(field, unit) {
                text[field] += String.fromCharCode(unit);
            },
            terminate(present, exception) {
                failure = new ManagedFailFastError(
                    present & 1 ? text[0] : null, present & 2 ? text[1] : null, exception);
            }
        },
        async instantiate(module, imports) {
            const instance = await WebAssembly.instantiate(module, imports);
            const exports = Object.create(null);
            for (const [name, value] of Object.entries(instance.exports))
                exports[name] = typeof value === "function" ? guard(value) : value;
            return {
                exports: Object.freeze(exports),
                promising(name) {
                    // WebAssembly.promising requires the original Wasm function.
                    return guard(WebAssembly.promising(instance.exports[name]), true);
                }
            };
        }
    };
}
