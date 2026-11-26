# Stage 109 · Virtual dispatch on sealed receivers

Run `npm run demo -- stage-109-exact-virtual-dispatch`. The inherited-slot demo
returns 16. The full CLR, Node and Edge comparisons run with `npm test`.

A virtual declaration supplies the CLI stack signature. Once stack analysis
proves a sealed class receiver, the compiler selects the body that implements
that virtual slot and retains the callvirt null check. The fixtures cover
inherited and declared overrides, a new slot and its override, the original
hidden slot, direct calls to an inherited implementation, and typed null
exceptions. A non-exact receiver keeps its diagnostic.

Explicit MethodImpl mappings with the same slot name and interface dispatch
still require additional runtime support. Slot rules follow
[ECMA-335](https://ecma-international.org/publications-and-standards/standards/ecma-335/)
Partition II 10.3. See [#238](https://github.com/SamG-Coder/StandardWasm/issues/238).
