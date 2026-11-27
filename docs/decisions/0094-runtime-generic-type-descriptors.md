# 0094 · Canonical generic interface Type descriptors

Status is proposed in [#288](https://github.com/SamG-Coder/StandardWasm/issues/288). The
first implementation is Stage 267.

The compiler recognizes the authentic `System.Type.MakeGenericType` call when
the receiver is a literal single-parameter open interface with no constraints
and its argument is a fresh one-element descriptor vector. The ordinary
CoreLib caller continues to allocate and fill the vector. A synthetic Wasm
function reads the actual element at runtime.

The factory is emitted after Object.GetType has discovered its descriptors.
It maps linked argument descriptors to their exact CLI generic keys and
reserves the corresponding closed interface layout. The same descriptor
global serves the generated result and any IL token for that type. Canonical
primitive keys take precedence over nominal layouts reserved for managed
method bodies. A literal-only TypeDef is registered at its call site so
layout failures retain that instruction's diagnostic.

The emitted helper uses the existing argument exception tags for null and
Void. The finite table covers reachable argument descriptors represented
when factory emission starts. Unknown descriptors and factory-produced
types used as further inputs without a linked descriptor remain unsupported.
Full dynamic generic construction also needs representation of open,
constrained and multi-parameter forms and the matching runtime operations
that consume those types.
