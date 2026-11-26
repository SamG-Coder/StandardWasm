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

ECMA-335 II.25.4.4 leaves local contents unspecified when the flag is clear.
Zeroing is one permitted outcome, so it refines the CLI semantics rather than
approximating them. C# reads only definitely assigned locals, so ordinary
compiled code cannot observe the difference.

`localloc` (`stackalloc`) memory is not a Wasm local. It is not decoded, and a
body that uses it still returns an explicit -2, whatever its header flag says.

## Verification

Stage 123 compiles ordinary C# under `[SkipLocalsInit]` and `AllowUnsafeBlocks`.
The Verify oracle first checks that each kernel has locals and a cleared flag.
It then compares scalar, value-type copy, nested-block and reference locals
against the CLR in Node and Edge, and confirms that a `stackalloc` method still
returns -2 with its output buffer untouched. Restoring the old rule makes
Verify fail at the first kernel (`Scalars compiler returned -2`).
