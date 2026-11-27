# Stage 299 · Read-only explicit value projections

Read a scalar from the zero-offset view of an explicit-layout value stored in a nested field. The compiler folds the field address, the exact CoreLib `Unsafe.AsRef` and `Unsafe.As` calls, and their immediate scalar load into ordinary field reads. It verifies the closed types, the declared overlay view, and control-flow boundaries before folding. The CLR copy or out-parameter store follows the original managed body.

```text
npm run demo -- stage-299-readonly-explicit-overlay
```

The same .NET Standard library is run on Windows x86 CLR and through StandardWasm in Node and Edge. Four results cover a closed generic out parameter, unsigned scalar bits, a floating-point view, and default and independently copied values. The test also checks that a read without a declared zero-offset view and a managed address that leaves the read sequence retain their located compiler diagnostics. Set `STANDARDWASM_MONO_RUNTIME` to the matching package's `runtimes/browser-wasm` directory to compare against stock Mono runtime metadata.

The Windows Forms startup checkpoint uses the original WinForms 10.0.10 `System.Value.TryGetValue<int>`. Windows x86 CLR reports an eight-byte union with its `Int32` field at offset zero. The containing sixteen-byte value holds its reference tag at byte zero and its union at byte eight. The supported load reads the four-byte scalar view and preserves the tag, other union bytes, and stored snapshots.
