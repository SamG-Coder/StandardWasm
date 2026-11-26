# 0016 · Platform references across linked calls

Proposed in [#124](https://github.com/SamG-Coder/StandardWasm/issues/124).

A linked module gives supported primitive arrays and strings the same
fixed Wasm GC types in each dependency. Pass the original references to
static callees and return those references directly. The dependency body
can use existing array, string, scalar and exception lowering.

Match the original CLI signature bytes before lowering storage types.
Signed and unsigned arrays can share a Wasm heap type while selecting
different overloads. Primitive array and string signature encodings
require no assembly-local type row translation. User-defined class
identity requires assembly-aware layout registration before it can cross
this boundary.

See [ECMA-335 signature encodings](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf).
