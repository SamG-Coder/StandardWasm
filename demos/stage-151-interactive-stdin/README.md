# Stage 151 · Interactive standard input

An ordinary .NET 10 console app prompts with `Console.Out.Write` and reads lines with
`Console.ReadLine()` until EOF. For each line it writes `hello <line>`, or `(empty)` for an
empty line. At EOF it writes `eof` to stderr and returns:

    lines + 10 × empty lines + 100 × UTF-16 code units read

That return value tells an empty line apart from EOF. The app is published unchanged, as an
apphost and as a single file.

Run it on Windows with the .NET 10 SDK, Node and Microsoft Edge:

```
npm run demo -- stage-151-interactive-stdin
```

The runner rebuilds the self-hosted compiler with `npm test`. `--no-rebuild` reuses
`artifacts/StandardWasm.Compiler.wasm`. Node 24 needs `--experimental-wasm-jspi`, and the
runner re-executes itself with it.

For each stdin input, the Windows processes, Node and Edge must agree on:

- the exit status
- the stdout bytes
- the stderr bytes

The inputs are:

- CRLF lines, including an empty one
- immediate EOF
- a final line without a terminator
- LF lines
- a lone CR
- `Grüße`, plus an emoji when the ANSI code page is UTF-8

In Edge, the CRLF inputs are typed into the page one line at a time, each after its prompt
appears, then closed with *End of input*. While each read waits, the page's own timer must
keep ticking.

To use the page directly, run `node demos/stage-151-interactive-stdin/run.mjs --serve` after
`npm test`. Drop the publish output, select **Run managed entry**, type lines, and select
**End of input** to close stdin.

The design is [decision 0079](../../docs/decisions/0079-interactive-standard-input.md). The
evidence is written to `artifacts/interactive-stdin/run-*/results.json`.
