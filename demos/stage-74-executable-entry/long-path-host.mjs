import { deviceComponent, maximumUnits, readPath } from "./win32-path-input.mjs";
import { resolveFullPath } from "./full-path-host.mjs";
import { foldWin32Name as fold } from "./win32-upcase.mjs";

const fileNotFound = 2, pathNotFound = 3, accessDenied = 5, notSupported = 50, filenameTooLong = 206;
const notDirectory = 267, noAccess = 998;
const invalidComponent = /[\\/:*?"<>|\u0000-\u001f]/u;
const dosName = /^[A-Z0-9!#$%&'()@^_`{}~\-]{1,8}(?:\.[A-Z0-9!#$%&'()@^_`{}~\-]{1,3})?$/i;

// Names compare through the Windows single-unit upcase table (win32-upcase.mjs).

// Windows x86 measurement (Stage 360): GetLongPathNameW looks up and rewrites only
// components whose input could be an 8.3 name by shape: no leading dot, at most
// one dot, at most 8 units before it and 3 after. Any characters qualify, including
// spaces, '+' and non-ASCII. Such a component takes its stored spelling (or the
// long name its alias stands for); every other component keeps the caller's spelling.
export function shortShaped(name) {
    if (name.length === 0 || name.startsWith(".")) return false;
    const dot = name.indexOf(".");
    if (dot < 0) return name.length <= 8;
    return dot <= 8 && name.indexOf(".", dot + 1) < 0 && name.length - dot - 1 <= 3;
}

export function validName(name) {
    return name.length > 0 && name.length <= 255 && !invalidComponent.test(name) &&
        !/[. ]$/u.test(name) && !deviceComponent.test(name);
}

function absoluteDirectory(path, aliases = []) {
    if (typeof path !== "string" || !/^[A-Za-z]:\\/u.test(path) || path.length > maximumUnits)
        throw new TypeError("StandardWasm: expected a canonical Win32 mount directory");
    const names = path.length === 3 ? [] : path.slice(3).split("\\");
    if (!names.every(validName))
        throw new TypeError("StandardWasm: expected a canonical Win32 mount directory");
    // rootDosNames[i] is the optional 8.3 alias of the i-th root component, as a
    // volume with short names enabled would have created for the mount's ancestors.
    if (!Array.isArray(aliases) || aliases.length > names.length ||
        !aliases.every(alias => alias == null || (typeof alias === "string" && dosName.test(alias))))
        throw new TypeError("StandardWasm: invalid long-path mount root aliases");
    const folded = names.map((_, index) => aliases[index] ? fold(aliases[index]) : undefined);
    return { drive: fold(path.slice(0, 2)), names, aliases: folded };
}

function childDirectory(node, names) {
    for (const name of names) {
        const child = node.children?.get(fold(name));
        if (!child?.children || fold(child.name) !== fold(name)) return undefined;
        node = child;
    }
    return node;
}

function indexMount(filesystem, mount) {
    const root = absoluteDirectory(mount?.root, mount?.rootDosNames ?? []), cwd = absoluteDirectory(filesystem.currentDirectory);
    const rootMatches = names => root.names.every((name, index) => fold(name) === fold(names[index] ?? ""));
    if (!Array.isArray(mount?.entries) || root.drive !== cwd.drive || !rootMatches(cwd.names))
        throw new TypeError("StandardWasm: expected a virtual current directory within the long-path mount");

    // Explicit entries are independent of the managed assembly and resource inventory.
    const tree = { children: new Map() };
    const entries = mount.entries.map(entry => {
        const names = typeof entry?.path === "string" ? entry.path.split("/") : [];
        if (names.length === 0 || !names.every(validName) ||
            (entry.kind !== "directory" && entry.kind !== "file") ||
            (entry.dosName !== undefined && (typeof entry.dosName !== "string" || !dosName.test(entry.dosName))))
            throw new TypeError("StandardWasm: invalid virtual long-path entry");
        return { names, entry };
    });
    entries.sort((left, right) => left.names.length - right.names.length);
    for (const { names, entry } of entries) {
        const parent = childDirectory(tree, names.slice(0, -1));
        if (!parent) throw new TypeError("StandardWasm: virtual long-path parent must be an explicit directory");
        const name = names.at(-1), key = fold(name), alias = entry.dosName && fold(entry.dosName);
        if (parent.children.has(key) || (alias && parent.children.has(alias)))
            throw new TypeError("StandardWasm: conflicting virtual long-path names");
        const node = { name, alias, readOnly: entry.readOnly === true,
            children: entry.kind === "directory" ? new Map() : undefined };
        parent.children.set(key, node);
        if (alias) parent.children.set(alias, node);
    }
    const directory = childDirectory(tree, cwd.names.slice(root.names.length));
    if (!directory) throw new TypeError("StandardWasm: virtual current directory is not a mount directory");
    return { root, tree, directory };
}

function resolvePath(path, mount) {
    if (/^ *$/u.test(path)) return { error: pathNotFound };
    const extended = path.startsWith("\\\\?\\") ? path.slice(0, 4) : "";
    const input = path.slice(extended.length);
    const drive = /^[A-Za-z]:/u.exec(input)?.[0] ?? "";
    const rest = input.slice(drive.length);
    const absolute = /^[\\/]/u.test(rest);
    if ((drive && fold(drive) !== mount.root.drive) || /^[\\/]{2}/u.test(rest) ||
        (extended && (!drive || !rest.startsWith("\\") || rest.includes("/"))) || !rest)
        return { error: notSupported };

    // Keep original separators and the spelling of components that are not short-shaped.
    const segments = rest.split(/([\\/]+)/u), spelled = segments.slice();
    let index = absolute ? 2 : 0;
    let node = absolute ? mount.tree : mount.directory;
    let parent;
    if (absolute) {
        // The virtual drive contains only the mount root's ancestors and the mount.
        // A path that ends on an ancestor names an existing directory; one that
        // leaves the ancestor chain names nothing, as on a real otherwise-empty volume.
        for (const [position, name] of mount.root.names.entries()) {
            const inputName = segments[index];
            if (inputName === undefined || inputName === "") return finish(ancestorDirectory);
            if (inputName === "." || !validName(inputName)) return { error: notSupported };
            const key = fold(inputName);
            if (key !== fold(name) && key !== mount.root.aliases[position])
                return { error: segments[index + 2] ? pathNotFound : fileNotFound };
            if (shortShaped(inputName)) segments[index] = name;
            index += 2;
        }
    }

    for (; index < segments.length && segments[index] !== ""; index += 2) {
        const name = segments[index];
        if ((name !== "." && !validName(name)) || (extended && name === ".")) return { error: notSupported };
        if (!node.children) return { error: pathNotFound };
        if (name === ".") continue;
        parent = node;
        node = node.children.get(fold(name));
        if (!node) return { error: segments[index + 2] ? pathNotFound : fileNotFound };
        if (shortShaped(name)) segments[index] = node.name;
    }
    return finish(node);

    function finish(found) {
        if (/[\\/]$/u.test(rest) && !found.children) return { error: notDirectory };
        const text = extended + drive + segments.join("");
        if (text.length > maximumUnits) return { error: filenameTooLong };
        // Windows x86 (Stage 360) writes the output as it meets each short-shaped
        // component: first everything since the previous write, as one piece, then
        // the respelled component. Text after the last short-shaped component, and a
        // path without one, is written only with the complete result.
        const pieces = [];
        let offset = extended.length + drive.length;
        for (const [position, piece] of segments.entries()) {
            if (position % 2 === 0 && piece !== "." && shortShaped(spelled[position])) {
                if (offset > (pieces.at(-1) ?? 0)) pieces.push(offset);
                pieces.push(offset + piece.length);
            }
            offset += piece.length;
        }
        return { text, node: found, parent, pieces };
    }
}

// The drive root and the mount root's ancestors exist but list nothing outside
// the mount. They are directories, so a deletion reports access denied.
const ancestorDirectory = Object.freeze({ name: "", children: new Map() });

export function createFilePathHost(memory, filesystem, errors) {
    const fail = error => { errors.system = error; return 0; };
    const configured = filesystem?.longPathMount;
    // The readiness of this tree only depends on the guest's mount inventory.
    // A deletion cannot access the browser origin or the machine's filesystem.
    const mount = filesystem && indexMount(filesystem, configured ?? { root: filesystem.currentDirectory, entries: [] });

    function getLongPathNameW(source, destination, bufferCapacity) {
        if (!configured) return fail(notSupported);
        const view = new DataView(memory.buffer);
        const address = source >>> 0, target = destination >>> 0, capacity = bufferCapacity >>> 0;
        const outputBytes = capacity * 2;
        if (capacity && (!target || outputBytes > view.byteLength - target)) return fail(noAccess);
        const input = readPath(view, address);
        if (input.error) return fail(input.error);
        // Exact in-place writes use the input snapshot. Other overlaps have no declared ABI.
        if (address !== target && capacity && address < target + outputBytes && target < address + input.bytes)
            return fail(notSupported);

        const path = resolvePath(input.path, mount);
        if (path.error) return fail(path.error);
        if (capacity <= path.text.length) {
            // A short buffer keeps the whole leading pieces that fit in capacity - 1
            // units. It is never terminated. Measured on Windows x86 (Stage 360).
            let written = 0;
            for (const end of path.pieces) {
                if (end > capacity - 1) break;
                written = end;
            }
            for (let index = 0; index < written; index++)
                view.setUint16(target + 2 * index, path.text.charCodeAt(index), true);
            return path.text.length + 1;
        }
        for (let index = 0; index < path.text.length; index++)
            view.setUint16(target + 2 * index, path.text.charCodeAt(index), true);
        view.setUint16(target + 2 * path.text.length, 0, true);
        return path.text.length;
    }

    function deleteFileW(source) {
        if (!mount) return fail(notSupported);
        const input = readPath(new DataView(memory.buffer), source >>> 0);
        if (input.error) return fail(input.error);
        if (/^ *$/u.test(input.path)) return fail(pathNotFound);
        const absolute = input.path.startsWith("\\\\?\\") ? { result: input.path } :
            resolveFullPath(input.path, filesystem.currentDirectory);
        if (absolute.error) return fail(absolute.error);
        const path = resolvePath(absolute.result, mount);
        if (path.error) return fail(path.error);
        const { node, parent } = path;
        if (node.children || node.readOnly) return fail(accessDenied);
        // Both keys refer to the same file. A single deletion removes its
        // long and DOS aliases for every subsequent operation in this launch.
        parent.children.delete(fold(node.name));
        if (node.alias) parent.children.delete(node.alias);
        return 1;
    }

    return { getLongPathNameW, deleteFileW };
}
