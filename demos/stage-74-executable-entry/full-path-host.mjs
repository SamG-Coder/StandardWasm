const noAccess = 998, invalidName = 123, filenameTooLong = 206, notSupported = 50;
const maximumUnits = 32766; // UNICODE_STRING content; leave room for NUL.
const invalidOrdinaryPath = /[<>|"*?\u0000-\u001f]/u;
const deviceComponent = /^(?:CON|PRN|AUX|NUL|CLOCK\$|COM[1-9¹²³]|LPT[1-9¹²³])(?:[. ]|$)/iu;

function canonicalDirectory(path) {
    if (typeof path !== "string" || path.length > maximumUnits ||
        !/^[A-Za-z]:\\(?:[^\\/:*?"<>|\u0000-\u001f]+(?:\\[^\\/:*?"<>|\u0000-\u001f]+)*)?$/u.test(path))
        return false;
    return path.slice(3).split("\\").every(part => !/[. ]$/u.test(part) && !deviceComponent.test(part));
}

function readPath(view, address) {
    if (address === 0) return { error: noAccess };
    let path = "";
    for (let index = 0; index <= maximumUnits; index++) {
        const offset = address + 2 * index;
        if (offset > view.byteLength - 2) return { error: noAccess };
        const unit = view.getUint16(offset, true);
        if (unit === 0) return { path, bytes: 2 * (index + 1) };
        path += String.fromCharCode(unit);
    }
    return { error: filenameTooLong };
}

function resolvePath(input, cwd, capacity) {
    if (/^ *$/u.test(input)) return { error: invalidName };
    const path = input.replaceAll("/", "\\");
    if (path === ".") {
        // The Windows drive-root fast path writes "D:" for capacities 3 and 4.
        const root = cwd.length === 3;
        return { result: root && capacity < 5 ? cwd.slice(0, 2) : cwd,
            required: root ? 3 : cwd.length + 1, part: root ? -1 : cwd.lastIndexOf("\\") + 1 };
    }

    let absolute = path;
    let minimumFallback = 0;
    const drive = /^[A-Za-z]:/u.test(path);
    if (drive && path[2] !== "\\") {
        const sameDrive = path[0].toUpperCase() === cwd[0].toUpperCase();
        const prefix = sameDrive ? (cwd.endsWith("\\") ? cwd : cwd + "\\") : path[0].toUpperCase() + ":\\";
        absolute = prefix + path.slice(2);
        // An unset per-drive directory first needs room for "D:\" and NUL.
        if (!sameDrive && capacity < 4) minimumFallback = 1;
    } else if (!drive && !path.startsWith("\\\\")) {
        const prefix = path.startsWith("\\") ? cwd.slice(0, 2) : (cwd.endsWith("\\") ? cwd : cwd + "\\");
        absolute = prefix + path;
    }
    const required = absolute.length + 1 + minimumFallback;
    if (absolute.length > maximumUnits) return { error: invalidName };

    let root, components;
    if (absolute.startsWith("\\\\")) {
        const [server, share, ...parts] = absolute.slice(2).split("\\");
        if (!server || !share || /[. ]$/u.test(server) || /[. ]$/u.test(share) || (server + share).includes(":"))
            return { error: notSupported };
        root = "\\\\" + server + "\\" + share;
        components = parts;
    } else {
        root = absolute.slice(0, 3);
        components = absolute.slice(3).split("\\");
    }
    // Ordinary DOS/UNC only. Extended and device namespaces are separate
    // contracts, as are alternate streams and undocumented names.
    if (invalidOrdinaryPath.test(absolute) || absolute.slice(root.length).includes(":"))
        return { error: notSupported };

    const normalized = [];
    let trailing = path.endsWith("\\");
    for (let index = 0; index < components.length; index++) {
        let part = components[index];
        if (!part || part === ".") continue;
        if (part === "..") { normalized.pop(); continue; }
        if (deviceComponent.test(part)) return { error: notSupported };
        if (/[. ]$/u.test(part)) {
            if (index !== components.length - 1) return { error: notSupported };
            part = part.replace(/[. ]+$/u, "");
            if (!part) { trailing = true; continue; }
        }
        normalized.push(part);
    }
    const joined = normalized.join("\\");
    const separator = !root.endsWith("\\") && (joined || trailing) ? "\\" : "";
    const result = root + separator + joined + (trailing && joined ? "\\" : "");
    const part = !joined || trailing ? -1 : result.lastIndexOf("\\") + 1;
    return { result, required, part };
}

function overlaps(left, leftBytes, right, rightBytes) {
    return leftBytes !== 0 && rightBytes !== 0 && left < right + rightBytes && right < left + leftBytes;
}

export function createFullPathHost(memory, filesystem, errors) {
    const fail = code => { errors.system = code; return 0; };
    if (filesystem === undefined) return () => fail(notSupported);
    const cwd = filesystem?.currentDirectory;
    if (!canonicalDirectory(cwd))
        throw new TypeError("StandardWasm: expected a canonical Win32 virtual current directory");

    // The directory is an instance-local provider value. This lexical query
    // never probes existence, the browser origin or a native filesystem.
    return function getFullPathNameW(source, bufferCapacity, output, filePart) {
        const view = new DataView(memory.buffer);
        const address = source >>> 0, target = output >>> 0, partSlot = filePart >>> 0;
        const capacity = bufferCapacity >>> 0, outputBytes = capacity * 2;
        if ((capacity && (!target || outputBytes > view.byteLength - target)) ||
            (partSlot && partSlot > view.byteLength - 4))
            return fail(noAccess);
        const input = readPath(view, address);
        if (input.error) return fail(input.error);
        if (overlaps(address, input.bytes, target, outputBytes) ||
            partSlot && (overlaps(address, input.bytes, partSlot, 4) || overlaps(target, outputBytes, partSlot, 4)))
            return fail(notSupported);
        const path = resolvePath(input.path, cwd, capacity);
        if (partSlot) view.setUint32(partSlot, 0, true);
        if (path.error) return fail(path.error);
        if (capacity === 0) return path.required;
        // Windows returns a leading NUL for short output. Bytes after that
        // terminator need not match its intermediate prefix copies.
        new Uint8Array(view.buffer, target, outputBytes).fill(0);
        if (capacity < path.required) return path.required;
        for (let index = 0; index < path.result.length; index++)
            view.setUint16(target + index * 2, path.result.charCodeAt(index), true);
        if (partSlot && path.part >= 0) view.setUint32(partSlot, target + path.part * 2, true);
        return path.result.length;
    };
}
