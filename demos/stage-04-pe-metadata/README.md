# Stage 04 · PE metadata discovery

Run `npm run demo -- stage-04-pe-metadata` after `npm ci`. The runner reads the built PE32+ PeLibrary.dll and passes those bytes to the Wasm export. Expected output is `Inspect(PeLibrary.dll, 0) = 0`. Zero means the locator found a bounded CLI metadata range and validated its signature.

PeLocator.cs is the portable implementation. ReaderDemo.cs adapts its status and result array to the test harness. JavaScript populates a Wasm GC byte array using the fixture exports CreateBytes and SetByte. This per-byte transfer exercises the actual bytes in the compiled reader. A production transfer API remains open.

The full suite compares status, metadata offset and size against .NET. It checks PE32 and PE32+ inputs and reports failures for malformed headers, CLI directories, sections and metadata ranges. The locator currently discovers the root. Metadata streams, tables and IL bodies are later stages.

The interface follows the [reader design](https://github.com/SamG-Coder/StandardWasm/pull/5).
