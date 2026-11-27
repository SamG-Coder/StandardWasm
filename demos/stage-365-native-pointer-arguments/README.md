# Stage 365 · Native pointer arguments

Run `npm run demo -- stage-365-native-pointer-arguments` on Windows with the .NET 10 SDK, the x86
runtime, Node and Microsoft Edge. Use `STANDARDWASM_COMPILER_PATH` to test a specific self-hosted
compiler. The runner records the compiler's SHA-256 in its `results.json`.

The fixture is an ordinary .NET Standard 2.0 library with `[DllImport]`s whose parameters and
results are unmanaged pointers. On the x86 CLR they call the real `kernel32` and `ucrtbase`, and
the results are the oracle. In Wasm, each `T*` is an i32 address into memory 0, passed to the
typed import unchanged. `scripts/native-imports.mjs` (`windowsPointers`) reads and writes that
memory through `state.memory` with the same C semantics. The kernels cover:

- `FileTimeToSystemTime(FILETIME*, SYSTEMTIME*)` for a leap day, the 1601 epoch and the largest
  accepted value, plus the top-bit failure: `ERROR_INVALID_PARAMETER` saved by
  `SetLastError = true`, with the output left untouched. A success after a failure reads back
  error 0.
- `SystemTimeToFileTime(SYSTEMTIME*, FILETIME*)`, a round trip, and an ignored `wDayOfWeek`. It
  rejects month 13, 29 February 2023, hour 24, year 1600 and 1000 ms, each with error 87 and the
  output untouched.
- `strlen(byte*)` over a fixed string, a fixed-size buffer and `stackalloc` memory
- `memcpy(void*, void*, size_t)` between fixed-size buffers, from a fixed string, from an `int`
  local, and between two struct locals. It checks the returned destination, a `byte**` copy of a
  pointer, and `memcpy(null, null, 0)` returning null.

All 22 values must match the CLR's in both engines.

Twelve signatures stay a located -2 at the import's MethodDef:

- `ref`/`out` and `in` byrefs
- `byte[]`, `string` and a delegate
- by-value `bool`, `char` and struct
- `bool*` and `char*`
- pointers to a `Pack = 1` struct and to an auto-layout struct

The CLR runs each of these kernels too, so the fixture is ordinary code on both sides.

Address creation is unchanged. The pointers come from `&` of native-frame locals, `fixed` strings,
fixed-size buffers addressed through a pointer, and `stackalloc`. Pinned managed arrays, direct
indexing of a managed-local fixed buffer and `&` of a by-value struct parameter remain
caller-side diagnostics owned by their own stages. The design is decision 0083, step 2.
