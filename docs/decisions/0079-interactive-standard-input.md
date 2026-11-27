# 0079 · Interactive standard input

Status is proposed in #282 (Stage 151). It extends decision 0039 (the console host
contract). Claude claimed it, and Codex agreed the stage and decision numbers were free
(handoff 080).

## Problem

`Console.ReadLine()` blocks its thread until a line or EOF arrives. A browser has no
blocking read, and the page must stay usable while a read waits. The same unchanged
console publish must still run on the CLR, in Node and in Edge.

## Mechanism: JS Promise Integration

`Console.ReadLine()` (the static `string ReadLine()` on the platform `System.Console`)
lowers to two imports in a new `standardwasm:console` group:

| Import | Type | Meaning |
| --- | --- | --- |
| `readLine` | `() -> i32` | the next line's length in UTF-16 code units, or -1 at EOF |
| `readUnit` | `(i32) -> i32` | code unit *i* of the line `readLine` last returned |

The module builds a fresh `String` from the units. EOF gives `null`.

- **Suspension.** The host wraps `readLine` in `WebAssembly.Suspending`. The module may
  suspend inside that call until input arrives, and to managed code it is an ordinary
  call.
- **Entry.** A module that imports `readLine` runs its entry through
  `WebAssembly.promising`, which returns a Promise.
- **Other modules.** The group follows the resource functions in the import index space.
  A module that only writes keeps its Stage 110 imports and synchronous exports.

Engine requirements: Edge 158 runs JSPI unflagged. Node 24.19 needs
`--experimental-wasm-jspi`, and the Stage 151 runner re-executes itself with that flag.

## Semantics

- **Lines.** A line ends at CRLF, LF or CR and excludes the terminator, as in
  `StreamReader.ReadLine`. A trailing CR waits for a possible LF unless input is
  closed. A final line without a terminator is returned before EOF.
- **Encoding.** Without a console, .NET decodes redirected stdin with the ANSI code
  page, as it does for output (decision 0039). The host decodes the same bytes with the
  same modelled encoding.
  - Code page 1252 maps its five undefined bytes (0x81, 0x8D, 0x8F, 0x90, 0x9D) to the C1
    code points of the same value in both directions, as .NET does.
  - UTF-8 without a preamble keeps a leading EF BB BF as U+FEFF in the line.

  Raw byte inputs check both properties as UTF-16 units (Codex review of 26fc3a4).
- **Ordering.** Writes are synchronous host calls between suspensions, so prompts,
  submitted lines and output keep the program's order. The browser worker streams each
  write to the page before the next read suspends. It keeps one ordered queue across both
  streams and merges only adjacent writes to the same stream, so stdout and stderr
  transitions keep their order (Codex review of 26fc3a4).
- **Exceptions.** A managed exception leaving the entry rejects the `promising` Promise
  with the same `WebAssembly.Exception`, and the launcher still reports it as unhandled.

## Hosts

- **Node:** the redirected stdin bytes feed a line source that is closed at the end.
- **Browser:** the Stage 151 page runs the entry in a worker. The page sends typed lines,
  terminated by CRLF like a Windows keyboard line, and an *End of input* button closes
  stdin. The worker's event loop stays free while the module is suspended. A main-thread
  timer shows the page stays responsive during a pending read.

## Verification

`npm run demo -- stage-151-interactive-stdin` publishes an ordinary .NET 10 app as an
apphost and as a single file. For each stdin input, the Windows processes, Node and Edge
must agree on the exit status and the stdout and stderr bytes. The inputs cover:

- CRLF, LF and lone-CR terminators
- an unterminated final line
- an empty line
- immediate EOF
- code-page-encodable Unicode, plus a non-BMP character on a UTF-8 code page

In Edge, each line is typed only after its prompt appears.

## Still diagnosed

- `Console.Read()`, `Console.In`, `TextReader` members, `Console.ReadKey` and `Console.KeyAvailable`
- `Console.InputEncoding` changes: the host models the code page only
