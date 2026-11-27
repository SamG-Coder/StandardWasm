# 0070 · Read-only character spans over strings

Use the immutable string's packed UTF-16 array as the traced owner of a `ReadOnlySpan<char>`. The full-string `MemoryExtensions.AsSpan(string)` overload preserves CLR length and indexing, including surrogate halves, unsigned code units and embedded NULs. Null yields the CLR's empty default. Indexing checks the logical length and a fused readonly load reads the original backing element.

Character and object spans share the Wasm descriptor shape but have distinct compiler signature types. The linker keeps these identities through managed calls between assemblies. Descriptors stay in managed frames, and the character span exposes no writable reference to string storage.

Recognize the canonical `MemoryExtensions` method definition or the official reference and its exact signature. Range and slice overloads, writable character spans and byref-like builders remain distinct work. The original CoreLib `String.JoinCore(ReadOnlySpan<char>, ReadOnlySpan<object>)` consumes both views.
