# 0027 · Native integer width for browser modules

Proposed in [#185](https://github.com/SamG-Coder/StandardWasm/issues/185).

The current browser target uses a 32-bit native integer width. The compiler
emits i32 for both native integer signature elements and retains a separate CLI
evaluation stack category. Signature binding still distinguishes IntPtr from
UIntPtr. The IL operation supplies signedness for comparisons, widening and
overflow checks. Array length produces a native stack value and supported arrays
accept native indices.

The target width is independent of the machine that runs the compiler. Stage 84
compares compiled DLL behavior against a CLR with a 32-bit native integer width.
An application that observes native width can produce different results on a
64-bit desktop. A future 64-bit target needs an explicit ABI covering data
layout, conversions, and managed and native call boundaries.

Framework assemblies whose managed bodies depend on native width must come from
a 32-bit pack, for example an x86 or browser-wasm CoreLib. The installed x64
CoreLib is suitable for metadata analysis in the WinForms startup probes.
Executing its native-width-specialized bodies would embed x64 assumptions in a
wasm32 module. For example, the x64 IL for `IntPtr.Size` hard-codes 8.
[#233](https://github.com/SamG-Coder/StandardWasm/issues/233) tracks runtime-pack
validation before enabling execution of those bodies.

JavaScript calls to exported native integer parameters use the same i32
conversion as exported `int` parameters, including wrapping to 32 bits.


This decision covers integer values. Managed references remain Wasm GC
references. The native integer representation leaves address interpretation,
method pointers, native calls and the memory model to their respective designs.

See [Stage 84](../../demos/stage-84-native-sized-storage/README.md) and
[ECMA-335, Partitions I and III](https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
