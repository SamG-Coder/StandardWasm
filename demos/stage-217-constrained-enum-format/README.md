# Stage 217 · Constrained enum formatting

Run `npm run demo -- stage-217-constrained-enum-format` with the Windows
x86 .NET 10 runtime, Node, and Edge. The runner compares the same .NET
Standard fixture across all three engines and checks that the generated
module needs no host imports. It records the results under
`artifacts/stage-217-constrained-enum-format/`.

The compiler handles the parameterless `System.Object.ToString()` virtual
slot when constrained to a resolved integral enum and called directly
through a frame argument or local. It reads the enum's names and numeric
constants from its defining assembly. All eight ordinary underlying
integer types have invariant decimal fallback. Named values use the same
cached string on repeated calls to that enum. Decimal strings for 0–299
share a cache across integer widths, matching CoreCLR 10.

Unique values may appear in any declaration order. For already sorted
aliases, the stage follows CoreCLR's first-match or midpoint search as
appropriate. Unsorted aliases and `[Flags]` enums report unsupported.
Culture-specific signs, boxing and formatting overloads are separate
boundaries.

The original `System.Drawing.Color.FromKnownColor` takes this constrained
path for values outside its known range before calling `Color.FromName`.
