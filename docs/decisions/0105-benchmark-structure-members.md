# 0105 · Members for the benchmark data structures

Status: proposed in #326 (Claude).

Decision 0104 covered the benchmark drivers. The nine data structures of
[How fast is C#?](https://devmobasa.github.io/how-fast-is-csharp/) also use unsafe code, `fixed`,
explicit layouts, inline arrays, rank-2 arrays, `UInt128` and the hardware vector types. Each
member below is recognized by its exact name, signature and platform identity. Every other use
stays a located diagnostic. [Stage 367](../../demos/stage-367-benchmark-structure-members/README.md)
compares all of them with the x86 CLR in Node and Edge.

## Arrays

- **Rank-2 arrays** of `int`, `uint`, `long` and `ulong` (zero lower bounds) use the element's
  ordinary vector, holding `rows * cols + 2` elements: `[0] = rows`, `[1] = cols`, and then the
  row-major elements. IL reaches such an array only through its runtime members, so it cannot be
  confused with a one-dimensional array of the same vector.
  - **Creation:** `.ctor(rows, cols)` raises `OverflowException` for a negative dimension. Combined
    with `InitializeArray`, the data segment receives the header and the elements directly.
  - **Access:** `Get` and `Set` check each index against its own dimension and raise
    `IndexOutOfRangeException`. An `Address` that is only read by a primitive `ToString()` is the
    same element read.
- **`Array.Fill<T>`** for a primitive `T` (both overloads) is one `array.fill`, after the CLR's
  `ArgumentNullException` and `ArgumentOutOfRangeException` checks.
- **`Array.Resize<T>(ref r.F, n)`** handles a primitive `T` and a class field in the exact shape C#
  emits (`ldarg; ldflda F; <size>; call`). The size expression must load only locals, fields and
  constants, so nothing can write `F` before the CLR would read it.
  - **Semantics:** a negative size raises `ArgumentOutOfRangeException`. The same length keeps the
    array and its identity. Otherwise `array.new_default` plus `array.copy` copies
    `min(length, n)` elements.
- **`Array.Copy` between packed arrays:** `byte[]`, `bool[]` and `sbyte[]` share one vector, as do
  `char[]`, `short[]` and `ushort[]`, and the CLR raises `ArrayTypeMismatchException` between
  them. A copy is lowered only when the instructions that produce both arrays (`ldfld`, `ldsfld`
  or `newarr`) prove the same element type.
- **`Array.Clone()`** on a one-dimensional array copies it into a new array of the same vector. A
  `castclass` or `isinst` to `int[]`/`uint[]`, `long[]`/`ulong[]`, `float[]` or `double[]` tests
  that vector; the CLR treats each signed/unsigned pair as one array type for casts too. Casts to
  the packed arrays stay diagnosed.
- **`Array.BinarySearch<T>(T[], T)`** for 32- and 64-bit integers ports `ArraySortHelper`'s probe
  order exactly (`lo + ((hi - lo) >> 1)`, then `~lo`), so the index it returns among equal
  elements matches the CLR.
- **`ldelema bool`** uses the packed byte element location. That location admits only `ldind.u1` and
  `stind.i1`, which are the CLR's `bool&` accesses. A `bool[]` element passed as a `ref bool`
  argument stays diagnosed. `sbyte[]` needs `ldind.i1`, so its element addresses stay diagnosed.

## Pinning (`fixed`)

Decision 0025 rejects copy-based pinning in general, because the CLR makes managed and pointer
accesses to a pinned array alias. This decision copies only where that aliasing is proved
impossible.

- **The copy:** the pinned address is a NativeMemory block (decision 0104) that holds a copy of
  the elements. The block is copied back and freed when the pinned local is cleared or
  overwritten, before every `ret`, and when an exception leaves the frame.
- **Block tracking:** each pin site keeps its block in a compiler-owned local, so arithmetic on the
  user's pointer variable cannot affect the copy-back. Two pins of the same array in one frame
  share one block.
- **The proof:** from the pin until the pinned local is cleared, no managed access to an array of
  the same storage may run. This holds for this method's records (a flow-sensitive walk that
  enters a handler only from its protected range) and for every method that code can call,
  scanned from raw IL.
  - **Fails the proof:** virtual, interface, delegate and indirect calls, `ldftn`, and platform
    members whose signatures carry arrays, byrefs, objects or spans. This includes calls to the
    console inside the pin, because `Console.SetOut` could route them to user code.
  - **Allowed:** an `ldelema` of a different primitive element type, a pin sequence, SIMD vector
    signatures, and the `GC` members.
  - **Native spans:** a `new Span<T>(void*, n)` stored in a local and then only `Fill`ed or
    `Clear`ed is native memory and cannot view an array.
- **Span pins:** `fixed (byte* p = span)` over an array-backed `ReadOnlySpan<byte>` or
  `Span<byte>` copies the span's own range. An empty span pins to null, as `GetPinnableReference`
  returns a null reference for it. Spans over module data keep the immutable path of
  `PinnedByteSpans`.
  - **Overlap:** two different byte pin locals in one frame could view overlapping ranges, which
    separate copies cannot model, so a span pin must be the frame's only byte pin.
- **Diagnosed:** anything that fails the proof stays a located diagnostic, including a nested pin
  of the same array in a callee.

## Layouts and values

- **Explicit-layout structs through pointers:** a blittable
  `[StructLayout(LayoutKind.Explicit, Size = n)]` struct (primitives and fixed buffers) gets its
  `sizeof` and field offsets from `FieldLayout`. The declared size must cover every field and be a
  multiple of the alignment, which is then the CLR's `sizeof`. Other declared sizes stay
  diagnosed.
  - **Managed copies:** when the fields are pairwise disjoint, the managed registry keeps them in
    independent slots, because disjoint fields never alias. Overlapping layouts keep the packed
    cells of decision 0058.
- **Flat inline arrays (skiplist):** a class field of `[InlineArray(N)]` struct `A`, whose element
  `E` has `k` fields of one 32- or 64-bit integer storage, is one flat vector of `N * k` elements,
  allocated (zeroed) on first use.
  - **Element references:** `ref E e = ref Unsafe.Add(ref Unsafe.As<A, E>(ref this.F), i)` becomes
    an i32 base index `i * k`. Then `e.X` is `this.F[base + ordinal(X)]`. `this` must not be stored
    in the method.
  - **Diagnosed:** any other use of `F` fails verification, because a vector appears where the CLI
    expects `A`. So does an address of an element field, for example a compound assignment.
- **`Math.Clamp`** for 32- and 64-bit integers raises `ArgumentException` when min > max.
- **`Buffer.MemoryCopy`** raises `ArgumentOutOfRangeException` when the count exceeds the
  destination size. It is one `memory.copy`, which handles overlap as `Buffer.Memmove` does.
- **`new Span<T>(void*, n).Fill(constant)` and `.Clear()`**, in the shape C# emits, become one
  native fill. A negative length raises `ArgumentOutOfRangeException`. A value whose bytes are all
  equal is a single `memory.fill`.
- **`T*&` parameters** use the native-int location carrier.
- **`Environment.FailFast(string)`** writes `Process terminated. <message>` to stderr and ends the
  process with `COR_E_FAILFAST` (0x80131623), as on Windows. The CLR's stack-trace lines are not
  reproduced.

## Hardware vectors

`Vector128<T>`, `Vector256<T>` and `Vector512<T>` for `T` = `int`, `uint`, `long` and `ulong` use
Wasm SIMD.

- **Representation:** every value is one immutable Wasm GC struct of four `v128` chunks; a
  `Vector128` uses chunk 0 and a `Vector256` chunks 0–1, with the rest zero. The verifier keeps
  the width and lane type as a tag.
  - **Pairs:** a `ValueTuple` of two equal vector types (`Widen`, tuple returns) is an immutable
    pair struct.
  - **Locals and pointers:** vector locals start as the zero vector, as CLI locals do. Pointers to
    vectors, `ldobj`/`stobj` and `sizeof` are supported.
  - **Diagnosed:** vector fields and arrays of vectors.
- **Members:** `Load`, `Store`, `Create`, `Equals`, the ordered comparisons, `ConditionalSelect`,
  `Min`, `Max`, `Abs`, `ExtractMostSignificantBits`, `Indices`, `Zero`, `One`, the `&` `|` `+`
  `-` `*` `<<` operators, `ShiftRightLogical`, `AsInt32`, `AsInt64`, `Widen`, `Shuffle`,
  `GetLower`, `GetUpper`, `ToScalar` and `Vector128.GetElement`.
- **Exactness:** every member is exact for every input.
  - Comparisons return all-ones lanes.
  - `ConditionalSelect` is `v128.bitselect`.
  - 64-bit `Min`/`Max` select by a signed compare.
  - Shifts mask the count, as .NET 8 and later do.
  - `Shuffle` returns zero for an index outside the vector.
  - `GetElement` raises `ArgumentOutOfRangeException` outside `[0, 4)`.
  - 64-bit unsigned compares have no Wasm instruction and stay diagnosed.
- **Hardware queries:** `IsSupported` and `IsHardwareAccelerated` still fold to `false` (decision
  0104).
  - **Remaining calls:** any other X86/Arm/Wasm intrinsic member that remains after pruning keeps its
    signature for verification and traps. On the CLR it raises `PlatformNotSupportedException`,
    which could be caught. These members appear only behind the false queries, as in the
    benchmarks, where Release IL can keep the query's value on the stack across a join.
  - **Constant locals:** a local that is only ever assigned that constant 0 folds at every load.

## UInt128 and its tuples

- **Representation:** `UInt128`, and a `ValueTuple<UInt128, int>`, is one immutable struct
  `{i64 lo, i64 hi, i32 extra}`. The tuple array is an array of nullable references to it, where
  null is the default tuple `(0, 0)`.
- **Arithmetic:** `op_Implicit(ulong)`, `op_Addition` and `op_Multiply` are exact modulo 2^128.
- **Tuple arrays:** tuple construction, `newarr` (`OverflowException` for a negative size), `stelem`
  and `ldelema; ldfld Item2` (`NullReferenceException`, `IndexOutOfRangeException`).
- **`Array.Sort(array, index, length)`** sorts by the default comparer: `Item1` unsigned, then
  `Item2` signed. Equal tuples are indistinguishable, so this heap sort leaves the same array as the
  CLR's introsort. The argument checks follow `Array.Sort`.

## Limitations

- **Uncatchable exceptions:** a call that remains to a hardware intrinsic member traps instead of
  raising a catchable `PlatformNotSupportedException`. `FailFast` prints no stack trace. Rank-2
  arrays beyond the Wasm array limit trap where the CLR raises `OutOfMemoryException`.
- **Formatting through pointers:** a primitive `ToString()` whose receiver is a raw pointer (for
  example `p[i].ToString()`) stays diagnosed; reading the element into a local works.
- **Pin restrictions:** the pin proof is conservative. Code inside a `fixed` block must not make
  virtual calls, and a frame with a span pin must have no other byte pin.
