# 0052 · Reserve closed generic base classes

A reference type's `extends` TypeSpec can name a closed generic class.
Resolve that class in the supplied assembly bundle and substitute each
`VAR` from the derived declaration's arguments. Those arguments are
available before the derived type has a layout. The reserved parent is
identified by its defining assembly, TypeDef and complete argument vector.

Reserve every ancestor before the child. Then complete inherited fields
in the context of the layout that declared them. This preserves Wasm
nominal subtype order and permits recursive field references from a base
or derived class after reservation. Dispatch of existing supported
ordinary virtual slots follows the resulting parent links.

Generic argument binding and direct inheritance share a bounded
reservation depth. A type argument that requires the still-unreserved
derived layout, such as `Derived : Base<Derived>`, receives a diagnostic.
Supporting that dependency needs a separate identity reservation strategy.

The installed x86 CoreLib's `SemaphoreSlim.TaskNode` uses this path
through its `Task<bool>` base. The base's result field uses Boolean and the
derived type's recursive fields retain their TaskNode identity.
