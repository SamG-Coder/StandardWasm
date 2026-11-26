# 0012 · Finally unwinding

Proposed in [#116](https://github.com/SamG-Coder/StandardWasm/issues/116).

A normal `leave` computes which cleanup ranges it exits, then saves the
continuation for each cleanup from inner to outer. The last continuation
holds the original branch destination. An exception instead saves its
Wasm exception reference in the selected cleanup's slot.

`endfinally` either selects the saved normal destination or rethrows
the pending reference with the IL offset still inside cleanup. Lexical
validation puts a protected range and its cleanup under the same
enclosing region. An exception escaping cleanup can therefore continue
through enclosing regions without selecting the same cleanup again. A
caught exception in a nested handler uses separate slots.

The portable backend currently supports catch and finally clauses without
filter side effects during handler selection. Native Wasm traps bypass the
exception dispatcher.

See [ECMA-335, Partition III §3.35](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
