# Stage 142 · Read-only character spans

Run `npm run demo -- stage-142-character-readonly-spans`. The original string's packed UTF-16 payload is read through `ReadOnlySpan<char>`. The example returns 65535 from an unsigned code unit.

The CLR comparison exercises individual code units (including surrogate halves and an embedded NUL), null and empty strings, typed bounds failures, and calls that carry character and object spans together, including a separately compiled companion DLL. The separate `CoreLibProbe.JoinOriginal` entry is for the original-framework investigation and remains compile-only. See [decision 0070](../../docs/decisions/0070-readonly-character-span.md).
