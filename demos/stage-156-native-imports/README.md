# Stage 156 · Native imports

Run `npm run demo -- stage-156-native-imports` on Windows with the .NET 10 SDK, the x86 runtime,
Node and Microsoft Edge. Build the self-hosted compiler with `npm test` first.

The fixture is an ordinary .NET Standard 2.0 library with `[DllImport]`s into real Windows DLLs:
`kernel32` (`MulDiv`, `SetLastError`) and `ucrtbase` (`abs`, `llabs`, `sqrt`, `fmaxf`, `roundf`,
`copysignf`). On the x86 CLR they call those DLLs, and the results are the oracle.

The compiler turns each reached P/Invoke into a typed WebAssembly import named after its DLL and
entry point, such as `native:kernel32` / `MulDiv`. In Node and Edge, `scripts/native-imports.mjs`
implements those imports with the same C semantics, and every value must match the CLR's. The
kernels cover:

- `MulDiv`, including rounding half away from zero, the 64-bit intermediate product, and -1 on
  overflow or a zero divisor
- `abs(int.MinValue)`, a 64-bit `llabs`, and `sqrt`
- `fmaxf` with a NaN operand, `roundf(-2.5f)` and `copysignf` with `-0`
- `SetLastError = true` read back through `Marshal.GetLastWin32Error`. The saved error comes
  through CoreLib's `Marshal.GetLastPInvokeError` InternalCall, which the host also implements.
- an entry point `kernel32` doesn't export. The CLR raises `EntryPointNotFoundException`; the
  host doesn't implement it either, so the module raises the same CoreLib exception and the
  kernel's `catch` returns 9 on both.

One case is a planned compile diagnostic. A `string` argument needs marshalling, which this step
doesn't do, so it stays a -2 located at the P/Invoke method.

Only blittable scalars are lowered for now. Pointers, structs, strings and `bool` come later. The
design is decision 0083.
