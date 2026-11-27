# 0077 · Fixed strings as frame-owned UTF-16 copies

Status is proposed in #275 (Stage 149). It builds on decision 0065 (the shadow
stack) and decision 0060 (native addresses in memory 0). Codex's WinForms trial
supplied the emitter. That trial is commits 4324ede and 7ec85cf: `Utf16MemoryWasm.Copy`
and `Pin`, and `StringMetadata.PinnableReference`. This stage replaces the trial's
whole-body match with a lowering of each pinning form wherever it appears.

## Semantics

`fixed (char* p = s)` gives the address of `s`'s UTF-16 code units, followed by
a NUL (ECMA-334 §23.7). A Wasm GC string is an `i16` array that memory 0 cannot
address, and Wasm GC objects never move. So pinning is a copy:

- A non-null string is copied, all code units plus a NUL, into a fresh block on
  the frame's shadow stack. `p` is the block's address. The empty string gets
  one NUL, and its address is non-null, as on the CLR.
- A null string gives address 0.
- The block stays reserved until the frame returns, normally or exceptionally,
  because Stage 137 releases the whole frame. The fixed statement may end earlier.
  Using `p` after the statement ends is undefined in C#, so the longer lifetime is
  a refinement.
- **Not written back.** A store through `p` changes the copy and not the string.
  ECMA-334 §23.7 says modifying a fixed string's characters gives undefined
  behaviour, because strings are immutable. A local `stind` diagnostic would not
  be sound: the pointer can reach a callee, for example through `PCWSTR` to
  `GetModuleHandleW`. It would also reject the common pattern that copies a fixed
  string into a `stackalloc` buffer. Writes are therefore documented, not diagnosed.

The block is 16-byte aligned. A string of 2^19 or more code units cannot fit and
raises an engine trap, as stack exhaustion does in decision 0065.

## IL forms

Roslyn emits one of two forms. Both Release and Debug builds are covered.

- **Without `string.GetPinnableReference`** (netstandard2.0):
  `stloc <pinned string>; ldloc; conv.u; stloc p; ldloc p; brfalse; ldloc p;
  call RuntimeHelpers::get_OffsetToStringData; add`.
- **With it** (.NET Core 3.0 and later):
  `brtrue; ldc.i4.0; conv.u; br; ldarg; call string::GetPinnableReference;
  stloc <pinned char&>`, then `ldloc; conv.u` wherever `p` is read.

The locals signature reader accepts `ELEMENT_TYPE_PINNED` only before `string`
(storage `String`) or `char&` (storage `Native`, the copy's address). Every other
pinned local stays a diagnostic. That includes `char[]` and other array pins,
and `&array[i]`, whose `ldelema` is a managed location (#271, #273).

`PinnedStrings.Lower` runs before linking:

| IL | Lowering |
| --- | --- |
| `ldloc <pinned string>; conv.u` | `PinString` [String] → [Native] |
| `call get_OffsetToStringData` in a method with a pinned string | `ldc.i4.0`: the copy has no object header |
| `call string::GetPinnableReference(); stloc <pinned char&>` | `PinString`, NullReferenceException on a null receiver, as the CLR dereferences it |

A pinned string local otherwise behaves as an ordinary `String` reference. A
pinned `char&` local behaves as an ordinary native address, and stores of
native zero release it. `OffsetToStringData` is implementation defined (8 on
x86, 12 on x64), and only the sum is meaningful.

`PinString` is record kind 118 and `PinnableReference` is string member 7. Both
match the integration line.

## Emission

`PinString` spills the string and tests it for null. If the string is non-null,
the emission unwraps the `Char[]`, allocates `2 * (length + 1)` bytes with
`StackAllocationWasm.Allocate` (which zeroes the terminator), and copies the code
units with `i32.store16`. The method uses the string scratch bank and the
shadow-stack globals, and imports `standardwasm:resources.memory`. As with
`localloc`, a method that runs a type-initializer guard cannot also pin a string.

## Verification

`npm run demo -- stage-149-fixed-strings` builds the same nine kernels for
netstandard2.0 and net10.0. The CLR runs both builds, and Node and Edge must
match 112 results plus 4 located diagnostics.

## Still diagnosed

- pinned arrays, pinned element and field addresses, and pinned `ref` locals of
  other types
- `fixed` over a custom `GetPinnableReference` (a non-string receiver)
- pinning in a method with a type-initializer guard
- releasing a copy at the end of its fixed statement. A loop that pins a long
  string many times can exhaust the 1 MiB shadow stack where the CLR would not.
  A later stage can release a pin's block when the pinned local is cleared, if
  that block is still the topmost allocation.
