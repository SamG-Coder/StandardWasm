# Stage 301 · Scoped pinning of static pointers

The fixture borrows the address of a static unmanaged pointer during its declaring type initializer. The addressed pointer is a native word in module memory. The initializer can observe its default value, write through the borrowed pointer, then read or update the same storage directly.

```text
npm run demo -- stage-301-static-pointer-pinning
```

The same ordinary .NET Standard assembly runs on Windows x86 CLR, Node, and Edge. Results are `101` for the synchronous helper write, `103` when an inline borrowed write and direct accesses share one location, and `109` when a native alias survives an intervening managed call. Attempting to publish the fixed address produces a compile diagnostic. The compiler recognizes the original `CoCreateInstance` output-pointer contract for borrow analysis. Browser COM activation still needs its host implementation.
