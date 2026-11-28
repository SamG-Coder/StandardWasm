# Stage 225 · Guarded string-to-span copies

Run `npm run demo -- stage-225-proven-string-span-moves` with Windows x86
.NET 10, Node and Edge. The three runtimes compare the same ordinary
fixture, including UTF-16 code units and alias-visible writes.
Evidence is written to `artifacts/stage-225-proven-string-span-moves/`.

The compiler recognizes the physical operands of a full UTF-16 copy
from a string into a writable character span. It requires the trusted
CoreLib declaring type, the exact physical backing fields and
`Buffer.Memmove<char>` signature, and a length guard dominating the
copy. No branch or exception boundary may enter the proof after its
first instruction.

The original methods retain their managed length branches. A short
destination is left untouched, and the method decides whether to
return false or throw `ArgumentException`. The tests exercise exact
fit, a larger slice, a shared view, empty spans, and short destinations.
