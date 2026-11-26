# 0066 · Inline-array value layouts

A valid platform `InlineArrayAttribute(int)` on a managed value expands its declared element field into a fixed number of Wasm GC fields. The first slot corresponds to the sole field row in the value's definition. Later slots have the same storage type and have no FieldDef tokens. Resolve generic element storage in the context of the closed value before filling the slots. Aggregate defaults and copies then use the same code as other managed values.

Read the attribute constructor and bounded payload from metadata. Accept auto and sequential values with one declared instance field and default packing. Limit the expanded slot count using the same workspace budget as other aggregates. Explicit layout, additional declared fields, and byte-level views require separate representation work.

The C# compiler generates inline arrays for collection expressions in framework code. For example, .NET 10 CoreLib uses a three-slot object inline array in `System.SR.Format(string, object, object)`. This decision supplies the layout for that value. Managed element addresses, lifetime rules, and spans are part of [issue #273](https://github.com/SamG-Coder/StandardWasm/issues/273).
