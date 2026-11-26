# Stage 142 · Read-only character spans

Run `npm run demo -- stage-142-character-readonly-spans`. The original string's packed UTF-16 payload is read through `ReadOnlySpan<char>`. The example returns 65535 from an unsigned code unit.

The CLR comparison exercises individual code units (including surrogate halves and an embedded NUL), null and empty strings, typed bounds failures, and calls that carry character and object spans together, including a separately compiled companion DLL. The separate `CoreLibProbe.JoinOriginal` entry is for the original-framework investigation and remains compile-only. See [decision 0070](../../docs/decisions/0070-readonly-character-span.md).

The fixed-string fixture covers the compiler-generated `fixed (char*)` form
when the pointer is used only for an immediate read. The native buffer belongs
to the current shadow-stack frame. Null stays address zero, while an empty
string gets a buffer containing a single zero code unit. The fixture also
checks a non-ASCII unit, a surrogate pair and a leading NUL.

This proof covers an immediate read within the current shadow-stack budget.
Passing the pinned pointer to native or managed calls requires a separate
lifetime check.
