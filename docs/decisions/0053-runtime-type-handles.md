# 0053 · Canonical runtime type handles

Resolve a TypeDef or TypeRef operand to the defining module and TypeDef
row. This pins the identity before any instance layout or type initializer
is needed. Store both parts in a private, immutable Wasm GC descriptor.
Every use of the same nominal type in a module reads the same global,
including references through linked assemblies.

Keep the CLI `RuntimeTypeHandle` value and `System.Type` reference distinct
during validation. They share a reference representation, so the platform
`Type.GetTypeFromHandle` operation retains the same descriptor or null for
the default handle. Descriptor identity is allocation identity. An ordinary
integer supplied as a Wasm `eqref` is distinct, including when the Type
reference is widened to `System.Object`.

Linked nominal System.Type signatures use the upstream layout at the module
boundary. An internal direct or virtual call can use the descriptor
representation for a parameter if every reached call supplies a descriptor
or null. Discovery triggers a new emission pass to check all callers against
the same representation. For a virtual call, the dispatcher and selected
overrides share the specialized argument ABI. Instance receivers keep their
nominal layout. Descriptors are also available for libraries without the
linked definition.

The supported runtime binder accepts the exact platform TypeRef and
`GetTypeFromHandle` signature. The raw Wasm ABI rejects these internal
types. Constructed type tokens, field and method handles beyond existing
array initialization, type boxing and other reflection operations retain
explicit diagnostics. `Type.Module` can use the defining module stored in
the descriptor when the runtime gains module metadata support.
