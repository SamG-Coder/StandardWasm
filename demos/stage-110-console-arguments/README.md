# Stage 110 · Console arguments and streams

An ordinary .NET 10 console app with `static int Main(string[] args)` is published unchanged, once with its apphost and once as a single file. The self-hosted compiler compiles it, and the launcher runs it with arguments. The app reverses the arguments into a new `string[]` and writes them with `Console.Out.Write` and `WriteLine`. It writes one line with `Console.Error.WriteLine` and returns the argument count plus ten times their UTF-16 length.

Run it on Windows with the .NET 10 SDK, Node and Microsoft Edge:

```
npm run demo -- stage-110-console-arguments
```

The runner rebuilds and self-recompiles the compiler with `npm test`. To reuse `artifacts/StandardWasm.Compiler.wasm` from a previous `npm test`, run `node demos/stage-110-console-arguments/run.mjs --no-rebuild` instead.

For each argument set in [expected.json](expected.json), the runner checks that the Windows processes, Node and Edge agree on:

- the exit status
- the stdout bytes
- the stderr bytes, line terminators included

The argument sets are: none, one empty argument, an argument with a space, `Grüße 😀` (non-BMP), and a mixed set. The Windows processes run without a console, so .NET encodes their output with the ANSI code page. The runner reads that code page from the registry and gives the browser host the same encoding and CRLF. On a Windows-1252 machine, the emoji therefore becomes `??` on every side. [Decision 0037](../../docs/decisions/0037-console-host-contract.md) defines the import and argument contract. The evidence is written to `artifacts/console-arguments/run-*/results.json`.

To use the browser page directly, run `node demos/stage-110-console-arguments/run.mjs --serve` after `npm test`. Drop the publish output, enter the arguments as a JSON array, choose the encoding and select **Run managed entry**.

`Vectors/` is the library fixture for the `npm test` oracle cases. It compares `string[]` allocation, element loads and stores, null elements, and typed bounds and null failures with the CLR. It also checks that Console members outside the contract are rejected with their IL offset.

Limitations:

- Interactive stdin, `Console.WriteLine` (the static overloads), formatting overloads, `Flush` and `Write(char)` are diagnosed as unsupported.
- Best-fit console encodings are not modeled.
- An unhandled managed exception is reported, but its CLR stderr text and exit status are not reproduced.
