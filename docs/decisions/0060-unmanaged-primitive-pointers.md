# 0060 · Primitive unmanaged pointers in host-lent memory

Status is proposed in #275. The memory interface was agreed with Codex
(handoff 073), and Stage 131 (#288) shares it.

The original `System.IO.UnmanagedMemoryStream` reads a manifest resource
through a `byte*` field (`Initialize(byte*, long, long, FileAccess)`, and
`ReadByte` through `ldind.u1`). Decision 0035 stored every pointer as a
null-only reference, so no pointer could be dereferenced.

## Representation

- **Memory.** Unmanaged pointers address memory 0. It is the single wasm32
  memory the host lends through the import `standardwasm:resources.memory`
  (at least one page, no maximum). A module imports it when a reachable body
  dereferences a pointer. Stage 131's resource lookup uses the same entry, so
  a module never has two memories. A memory import takes no function index.
- **Pointers.** A single-level pointer to a primitive (`bool`, `char`, the
  sized integers, `float`, `double`, `nint`, `nuint`) is an i32 address. This
  is the CLI native-int stack type (ECMA-335 III.1.1.5).
- **`void*`.** Since Stage 137, a single-level `void*` is the same address. A
  `byte*` passes to a `void*` parameter with no IL conversion. The original
  `UnmanagedMemoryStream.Initialize` relies on this when it calls
  `ArgumentNullException.ThrowIfNull(void*, string)` (Codex, handoff 087).
  Pointers to named structs and multi-level pointers keep the null-only storage
  of decisions 0032 and 0035 until their own lowering. Signature binding still
  compares pointer levels and the terminal encoding.
- **Arithmetic.** Native int combined with int32 or native int gives native
  int for arithmetic and bitwise operations, and a shift keeps the type of
  its value (ECMA-335 Table III.2). C# emits explicit element scaling
  (`conv.i; ldc.i4 4; mul; add`). On wasm32 these are the ordinary i32
  operations.

## Loads and stores

- `ldind.i1/u1/i2/u2/i4/u4/i8/i/r4/r8` and `stind.i1/i2/i4/i8/i/r4/r8` become
  memory-0 loads and stores. Small integers sign- or zero-extend to int32 as
  the opcode says. A memarg has alignment `2^0`, because C# pointers need not
  be aligned, and offset 0.
- `stind.r4` and `stind.r8` convert between f32 and f64 when the value has
  the other width.
- Only an unmanaged pointer (a native int on the stack) addresses memory.
  A null literal used directly as an address, such as `*(byte*)null`, is a
  proven-null native int and raises the typed exception at run time. `ldind.ref`,
  `stind.ref` and managed byrefs stay located diagnostics.
- **Null.** Address 0 is null on the CLR and here, and the host never places
  data at 0. Each load and store tests its address and raises the typed
  `NullReferenceException` for 0. The store test runs after both operands are
  evaluated, as on the CLR.
- **Other bad addresses.** Any other address outside the memory is a Wasm
  engine trap. That stands in for the CLR's access violation, which ends the
  process. It is never reported as a typed managed exception.

## Still diagnosed

- `fixed` and pinning of managed objects (decision 0025)
- `stackalloc` (decision 0051)
- `calli` and function pointers
- `sizeof` and pointers to structs
- `void*` dereference
- checked native arithmetic
