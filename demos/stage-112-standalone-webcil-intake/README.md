# Stage 112 · Standalone Webcil intake

Run `npm test` to build the self-hosted compiler. Then run
`npm run demo -- stage-112-standalone-webcil-intake` with the .NET 10 SDK,
Node and Microsoft Edge. The runner publishes the SDK's ordinary standalone
template under `artifacts/standalone-webcil/`. The original stock host opens
Counter and increments it from 0 to 1. The runner then selects the same publish
through the StandardWasm page and compares its compilation diagnostic to Node.

The intake reads the JSON embedded in the SDK's fingerprinted .NET 10 boot
loader. It verifies SHA-256 for every declared managed asset, keeps the original
paths and bytes, and extracts the two passive WebAssembly data segments as
documented in the [Webcil specification][webcil]. The app entry and managed
companions flow through the same assembly-bundle compiler API as the console
and WinForms intake. Compiler metadata and method-body reads map Webcil RVAs
against the original payload's section table.

The runner compares the published entry token and metadata to the SDK's linked
PE. It also checks that a missing resource, a changed resource and a native
runtime Wasm substitution are rejected. Results are saved as `results.json`
under the run directory. The comparison records the current compiler boundary
before application execution. Stage 130 admits the fieldless
`System.Threading.Tasks.VoidTaskResult` in the async `Program::<Main>$`.
The current boundary is `-2 [0, 1, 30]` at IL offset `0x1e`, where
`ldflda` takes the address of the nested builder for
`AsyncTaskMethodBuilder.Start`. The preceding local state-machine address
compiles. Localized satellite assemblies,
alternate boot formats and application rendering through StandardWasm
remain future work.

[webcil]: https://github.com/dotnet/runtime/blob/v10.0.10/docs/design/mono/webcil.md
