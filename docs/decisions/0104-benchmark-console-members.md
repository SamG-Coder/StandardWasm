# 0104 · Platform members for benchmark console programs

Status: proposed in #326 (Claude).

The C# programs of [How fast is C#?](https://devmobasa.github.io/how-fast-is-csharp/) are
ordinary .NET 10 console apps: a driver that times data-structure workloads and prints one JSON line
per sample. Running them unchanged needs platform members that the console path
(`Microsoft.NETCore.App` built in, decision 0080) did not lower. Each member below is recognized by
its exact name, signature and platform identity, as the members of decision 0003 are. Anything else
stays a located diagnostic. [Stage 366](../../demos/stage-366-benchmark-console-members/README.md)
compares all of them with the x86 CLR in Node and Edge.

## Strings and formatting

- `String.op_Inequality(string, string)` negates the existing ordinal comparison.
- `ToString()` on `SByte`, `Byte`, `Int16`, `UInt16`, `Int32`, `UInt32`, `Int64` and `UInt64`
  produces the invariant decimal text. It shares the digit loops of the enum fallback. Integer
  `ToString()` uses the current culture's `NegativeSign`. The browser host models the invariant
  culture, so the sign is always `-`. When the receiver is `ldloca` or `ldflda`, including of an
  init-only field, the value is loaded directly. Otherwise it is read through its managed location.
- `String.Concat(string[])` follows the CoreLib body. A null array throws
  `ArgumentNullException`. Zero or one element returns without copying. Null elements count as
  empty, and a zero total returns `String.Empty`. `Concat` of three or four strings chains the
  two-string join, which keeps the CLR's content and reference identity. A total above
  `Int32.MaxValue` traps where the CLR throws `OutOfMemoryException`. No engine can hold such inputs.
- `Char.IsWhiteSpace(char)` is the fixed Unicode 15.1 set: U+0009–U+000D, U+0020, U+0085, U+00A0,
  U+1680, U+2000–U+200A, U+2028, U+2029, U+202F, U+205F and U+3000.

## Console, clock and process

- `TextWriter.Flush()` on `Console.Out` or `Console.Error` emits no code. The CLR console writers
  auto-flush, and the host receives every code unit synchronously.
- `Stopwatch.GetTimestamp()` calls the new import `standardwasm:clock` `timestamp: () -> i64`,
  which returns monotonic nanoseconds. `Stopwatch.Frequency` is therefore the constant
  1,000,000,000, the value .NET reports on Linux. Windows reports the QPC rate, so tick values
  differ, but durations converted through `Frequency` agree. `Stopwatch.IsHighResolution` and
  `BitConverter.IsLittleEndian` are `true`.
- `Environment.Exit(int)` calls `standardwasm:process` `exit(status)` and then traps. On the CLR no
  `finally` or `catch` block of the calling thread runs. A trap is the one unwind that Wasm
  exception handling cannot intercept, so this matches. The launcher reports the recorded status.
- New import groups follow every existing group, so earlier import indices are unchanged.

## Native memory

`NativeMemory` resolves through `System.Runtime.InteropServices` with a framework key token. Its
blocks live in memory 0 (decision 0060), in pages the host heap grows for itself. That keeps them
apart from the shadow stack (decision 0065) and resource pages.

- `Alloc`, `AllocZeroed`, `AlignedAlloc`, `Realloc` and `AlignedRealloc` call `standardwasm:native`
  `alloc(size, alignment, zero)` and `realloc(address, size, alignment)`.
- `Free` and `AlignedFree` call `free(address)`.
- `Clear`, `Fill` and `Copy` are `memory.fill` and `memory.copy`.
- A zero-byte request allocates one byte, as .NET does. A non-power-of-two alignment throws
  `ArgumentException`.
- Allocation failure and an element-count product above `nuint.MaxValue` trap where the CLR throws
  `OutOfMemoryException`. A nonzero `Clear`, `Fill` or `Copy` at address 0 traps for the CLR's
  access violation.

`demos/stage-110-console-arguments/host/native-heap.mjs` uses power-of-two size classes with free
lists. Each block carries an 8-byte header that records its start and class.

## Arithmetic, bits and arrays

- `Math.BigMul(ulong, ulong, out ulong)` and the exact `(ulong)((UInt128)a * b / c)` form compute the
  full 128-bit product. The form uses only evaluation-stack temporaries. The division is a 128-by-64
  long division that keeps the low 64 quotient bits, and a zero divisor throws
  `DivideByZeroException`. Other `UInt128` uses still need the type's own layout.
- `BitOperations.LeadingZeroCount`, `TrailingZeroCount` and `PopCount` are `clz`, `ctz` and `popcnt`.
  The native-int overloads count 32 bits on wasm32, as on the 32-bit CLR.
- `IntPtr.Size` is 4 and `UIntPtr.MaxValue` is `0xffffffff` (decision 0027).
- `Array.Sort<T>(T[])` for `int`, `uint`, `long` and `ulong` is an in-place heapsort. Equal integer
  keys are indistinguishable, so any correct sort yields the CLR's array. Other element types stay
  diagnosed.
- `ldelema` on `uint`, `long` and `ulong` elements shares the Int32 and Int64 cells, and `byte`
  elements use the writable byte location.
- `MemoryMarshal.GetArrayDataReference<T>` gives the (array, 0) location for `int`/`uint` and
  `long`/`ulong` elements. It is null-checked but not bounds-checked, as on the CLR.
  `Unsafe.Add<T>(ref T, int)` offsets that location. An access outside the array traps where the
  CLR's behavior is undefined.
- `Unsafe.SkipInit<T>` is a no-op. `Unsafe.BitCast<TFrom, TTo>` reinterprets equal-size primitives
  and normalizes narrow integer results.

## Hardware feature queries

The following properties are constant `false`:

- `IsSupported` on `System.Runtime.Intrinsics.X86`, `Arm` and `Wasm` classes, including nested `X64`
  and `Arm64`
- `Vector64`, `Vector128`, `Vector256` and `Vector512.IsHardwareAccelerated`
- `System.Numerics.Vector.IsHardwareAccelerated`

.NET reports exactly this on hardware without those instruction sets, and callers must keep a
software path. A `brtrue` or `brfalse` consuming the query is resolved, and unreachable code is
removed before classification, so accelerated paths are never lowered. A generic local of a type
this target cannot lay out (such as `Vector512<long>`) gets an `i32` placeholder. Any live load,
store or address of it is still rejected at its IL offset.

## Exceptions and the collector

- `newobj` of `ArgumentException`, `ArgumentNullException` or `ArgumentOutOfRangeException`
  followed by `throw` becomes the fixed typed throw of decision 0054. The constructor arguments are
  evaluated and discarded, so `Message` and `ParamName` are not carried.
  `InvalidOperationException` and `OutOfMemoryException` have no fixed tag and trap. An unhandled
  exception ends the CLR process without running `finally`, so the difference is observable only if
  a caller catches one.
- `GC.KeepAlive` consumes its argument, exactly. `GC.Collect()` and `GC.WaitForPendingFinalizers()`
  return without running finalizers in modules without finalizer tracking (Stage 152). A finalizer's
  side effects, such as freeing `NativeMemory`, therefore do not happen. Long benchmark runs that
  rely on finalizers to release native memory grow the heap. Real finalization is open work.

## Still diagnosed

The benchmarks also use the following, each with its own issue:

- pinning of managed arrays (`fixed` over `long[]`)
- `Span<T>(void*, int)`
- multi-dimensional arrays
- inline-array struct element references
- `sizeof` of structs with fixed buffers
- `UInt128` in tuples
- the remaining `Vector128`/`Vector512` arithmetic outside feature checks
