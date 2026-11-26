# 0051 · Zero-initialize locals in [SkipLocalsInit] methods

Status: proposed (#268).

## Context

A method body whose header clears `CorILMethod_InitLocals` was rejected with
-2 whenever it declared locals. The .NET runtime and Windows Desktop libraries
are built with `[SkipLocalsInit]`. In the Stage 117 census (#252) of the
unchanged Stage 72 WinForms app, this single rule was the first blocker for
3,788 reached methods, and none of the 6,011 reached methods that carry the
flag could compile.

## Decision

The compiler no longer requires the flag. Every local in a compiled body
starts zeroed, whether or not the IL asks for it:

- **Scalars** start at zero, because Wasm locals start at zero.
- **References** start null. The CLR zeroes GC references even without the
  flag, so this matches the CLR exactly.
- **Value-type locals** start null, which the compiler already treats as the
  zeroed aggregate (as `newobj` on a value type does).

When the flag is set, ECMA-335 II.13.2 guarantees zeroed locals, and that is
exactly what the compiler produces. When it is clear, II.25.4.4 leaves local
contents unspecified. Zeroing is one permitted outcome, so it refines the CLI
semantics rather than approximating them. Code that reads only definitely
assigned locals, like this stage's fixtures and ordinary safe C#, sees the same
values either way. Code that deliberately reads a slot before writing it can
observe the difference: `Unsafe.SkipInit`, unsafe pointer reads of a local, or
hand-written IL. On the CLR such code sees whatever the stack held, which no
program can rely on, and here it consistently sees zero.

`localloc` (`stackalloc`) memory is not a Wasm local. Decision 0065 (Stage 137)
places it on a shadow stack in memory 0. The block is zeroed whatever the header
flag says, which is the same refinement as for locals.

## Verification

Stage 123 compiles ordinary C# under `[SkipLocalsInit]` and `AllowUnsafeBlocks`.
The Verify oracle first checks that each kernel has locals and a cleared flag.
It then compares scalar, value-type copy, nested-block and reference locals
against the CLR in Node and Edge. Its `stackalloc` method returned -2 until
Stage 137, and it now compares with the CLR. Restoring the old rule makes
Verify fail at the first kernel (`Scalars compiler returned -2`).
