# Stage 25 · Switch dispatch

Run `npm run demo -- stage-25-switch-dispatch` from the root after `npm ci`. Expected output is `TokenizeSample(-1, 0) = 20203`. The tokenizer finds 2 numbers, 2 identifiers and 3 operators in `x1 + 42*y + 7`.

Dispatch.cs contains the jump-table shapes Roslyn produces: a dense `switch` (`DayKind`), negative and gapped labels that Roslyn biases by the lowest label (`Biased`), a switch expression (`Weight`, which needs `LangVersion` latest but still targets netstandard2.0), `goto case` re-entry (`Collatzish`), a nested-switch tokenizer state machine over an `int[]` (`Tokenize`), and a 64-entry table (`Wide`). The compiler lowers each IL `switch` to a Wasm `br_table` whose index is read as unsigned. That matches ECMA-335 III.3.66: an out-of-range value, including a negative one after biasing, falls through to the default.

`npm test` compares every export with the same DLL on the CLR. It covers each label, the values just outside every range, `int.MinValue`/`int.MaxValue` and seeded random inputs.

Limitation: control flow still runs through the method's block dispatcher (`$pc` plus `br $dispatch`), so each switch target is one dispatcher iteration away. Structured control-flow recovery remains future work.
