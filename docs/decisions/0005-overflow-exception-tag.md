# 0005 · Checked overflow at the WebAssembly boundary

Status: proposed in [#100](https://github.com/SamG-Coder/StandardWasm/issues/100).

Modules with reachable checked integer arithmetic or fixed-width checked conversions define one zero-parameter exception tag for `System.OverflowException`. The export name is `"\u0000overflow"`. The leading NUL makes the name independent of the printable managed export manifest. The module defines the tag locally and requires no runtime imports.

The host identifies checked overflow by comparing the exception with the tag of the originating instance through `WebAssembly.Exception.is`. The compiler validates floating-point ranges before executing a trapping integer conversion. Signed multiplication guards the exceptional WebAssembly division case separately. Unrelated runtime traps retain their existing behavior even when the module exports this tag.

This contract identifies the failure type at the host boundary. CIL catch regions, inheritance-based matching, exception objects, and CLR exception properties require additional runtime support.
