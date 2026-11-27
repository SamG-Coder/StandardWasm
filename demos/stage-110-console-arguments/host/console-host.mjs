// Host side of decision 0039: console streams and Main(string[]) arguments.
// The generated module writes UTF-16 code units to stream 1 (stdout) or
// 2 (stderr) and asks the host for a line terminator. The host owns what
// the operating system owns for the CLR: Console.OutputEncoding and
// Environment.NewLine. Both are explicit options so a comparison can name
// the Windows configuration it reproduces.

export const consoleImportModule = "standardwasm:console";

// Windows-1252 bytes 0x80-0x9f. Zero marks the five bytes the code page
// leaves undefined. Other BMP characters below U+0100 encode as themselves.
const windows1252High = [
    0x20ac, 0, 0x201a, 0x0192, 0x201e, 0x2026, 0x2020, 0x2021, 0x02c6, 0x2030, 0x0160, 0x2039, 0x0152, 0, 0x017d, 0,
    0, 0x2018, 0x2019, 0x201c, 0x201d, 0x2022, 0x2013, 0x2014, 0x02dc, 0x2122, 0x0161, 0x203a, 0x0153, 0, 0x017e, 0x0178
];

function encodeWindows1252(units) {
    const bytes = new Uint8Array(units.length);
    units.forEach((unit, index) => {
        let byte = -1;
        if (unit < 0x80 || unit >= 0xa0 && unit <= 0xff) byte = unit;
        else if (unit >= 0xd800 && unit <= 0xdfff) byte = 0x3f; // .NET writes '?' for each surrogate code unit.
        else if (windows1252High.includes(unit)) byte = 0x80 + windows1252High.indexOf(unit);
        // .NET applies Windows best-fit mappings to other characters. They are
        // not modeled, so report them instead of guessing a replacement byte.
        if (byte < 0) throw Error("U+" + unit.toString(16).padStart(4, "0") +
            " has no exact Windows-1252 byte. Best-fit console encoding is not modeled.");
        bytes[index] = byte;
    });
    return bytes;
}

// UTF-8 as .NET's UTF8Encoding writes it: no preamble, and U+FFFD for each
// unpaired surrogate. TextEncoder implements the same replacement.
function encodeUtf8(units) {
    let text = "";
    for (let i = 0; i < units.length; i += 4096) text += String.fromCharCode(...units.slice(i, i + 4096));
    return new TextEncoder().encode(text);
}

const encoders = { "utf-8": encodeUtf8, "windows-1252": encodeWindows1252 };

// Stage 151 (decision 0079): standard input. Console.InputEncoding follows the same
// configuration as the output, so redirected stdin bytes decode as the CLR decodes them.
function decodeWindows1252(bytes) {
    let text = "";
    for (const byte of bytes)
        text += String.fromCharCode(byte >= 0x80 && byte <= 0x9f ? windows1252High[byte - 0x80] || 0xfffd : byte);
    return text;
}
const decoders = {
    "utf-8": bytes => new TextDecoder("utf-8").decode(bytes),
    "windows-1252": decodeWindows1252
};

// A line source for Console.ReadLine. push() accepts decoded text holding any number of
// lines or part of one; close() marks EOF. Lines end at CRLF, LF or CR, as in
// StreamReader.ReadLine, and a final unterminated line is still returned before EOF.
export function createLineInput() {
    let buffer = "", closed = false, waiter = null;
    function take() {
        const match = /\r\n|\n|\r/.exec(buffer);
        // A trailing CR may be the first half of CRLF: wait for more unless closed.
        if (match && !(match[0] === "\r" && match.index === buffer.length - 1 && !closed)) {
            const line = buffer.slice(0, match.index);
            buffer = buffer.slice(match.index + match[0].length);
            return line;
        }
        if (closed) {
            if (buffer.length === 0) return null;
            const line = buffer;
            buffer = "";
            return line;
        }
        return undefined;
    }
    function wake() {
        if (!waiter) return;
        const line = take();
        if (line === undefined) return;
        const resolve = waiter;
        waiter = null;
        resolve(line);
    }
    return {
        push(text) { if (closed) throw Error("Standard input is already closed."); buffer += text; wake(); },
        close() { closed = true; wake(); },
        next() {
            const line = take();
            if (line !== undefined) return Promise.resolve(line);
            if (waiter) throw Error("Only one Console.ReadLine can wait at a time.");
            return new Promise(resolve => { waiter = resolve; });
        }
    };
}

// The whole of a redirected stdin, decoded with the console encoding.
export function inputFromBytes(bytes, encoding) {
    const decode = Object.hasOwn(decoders, encoding) ? decoders[encoding] : undefined;
    if (!decode) throw Error("Unsupported console encoding " + encoding + ".");
    const input = createLineInput();
    input.push(decode(bytes));
    input.close();
    return input;
}

export function createConsoleHost({ encoding = "utf-8", newLine = "\r\n", input = null, onWrite = null } = {}) {
    const encode = Object.hasOwn(encoders, encoding) ? encoders[encoding] : undefined;
    if (!encode) throw Error("Unsupported console encoding " + encoding + ".");
    if (newLine !== "\r\n" && newLine !== "\n") throw Error("Environment.NewLine must be CRLF or LF.");
    // Code units accumulate per stream and are encoded once. This matches the
    // CLR's stateful encoder, which keeps a surrogate pair split across two
    // Write calls together.
    const streams = { 1: [], 2: [] };
    function stream(number) {
        const units = streams[number];
        if (!units) throw Error("Unknown console stream " + number + ".");
        return units;
    }
    // onWrite sees each code unit as it is written, so an interactive page can show a
    // prompt before the read that follows it. Writes are synchronous host calls, so their
    // order relative to reads is the program's order.
    function put(number, unit) {
        stream(number).push(unit);
        if (onWrite) onWrite(number, unit);
    }
    let line = "";
    const consoleImports = {
        write(number, unit) { put(number, unit & 0xffff); },
        newLine(number) { for (let i = 0; i < newLine.length; i++) put(number, newLine.charCodeAt(i)); },
        // Code unit i of the line the last readLine returned.
        readUnit(index) { return line.charCodeAt(index); }
    };
    if (input) {
        if (typeof WebAssembly.Suspending !== "function")
            throw Error("Console.ReadLine needs JS Promise Integration (WebAssembly.Suspending).");
        // The module suspends here until a line or EOF is available (decision 0079).
        consoleImports.readLine = new WebAssembly.Suspending(async () => {
            const next = await input.next();
            if (next === null) return -1;
            line = next;
            return line.length;
        });
    }
    const imports = { [consoleImportModule]: consoleImports };
    return {
        imports,
        encoding,
        newLine,
        output: () => ({ stdout: encode(streams[1]), stderr: encode(streams[2]) })
    };
}

// Build the managed string[] through the module's exported constructors.
export function buildArguments(exports, values) {
    if (!Array.isArray(values) || values.some(value => typeof value !== "string"))
        throw Error("Entry arguments must be an array of strings.");
    const create = exports["\0newArguments"], newString = exports["\0newString"];
    const setUnit = exports["\0setStringCodeUnit"], setArgument = exports["\0setArgument"];
    if ([create, newString, setUnit, setArgument].some(f => typeof f !== "function"))
        throw Error("The module does not export the string[] argument constructors.");
    const array = create(values.length);
    values.forEach((value, index) => {
        const characters = newString(value.length);
        for (let i = 0; i < value.length; i++) setUnit(characters, i, value.charCodeAt(i));
        setArgument(array, index, characters);
    });
    return array;
}

export const hex = bytes => Array.from(bytes, value => value.toString(16).padStart(2, "0")).join("");
