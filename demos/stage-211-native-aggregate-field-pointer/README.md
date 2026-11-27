# Stage 211 · Native aggregate field pointers

The compiler recognizes a static helper whose original body addresses a native-word field and returns that pointer through the platform Unsafe operations. A local owner with a verified sequential native layout gets one shadow-stack cell. Normal typed operations and every pointer borrow use its bytes.

Run `npm run demo -- stage-211-native-aggregate-field-pointer`. Supply a prebuilt compiler with `STANDARDWASM_COMPILER_PATH` and `--no-rebuild` to pin verification.

The fixture compares a nonzero field offset, a clear through a second borrow, an earlier value copy, distinct owners and typed replacement of an already borrowed owner in Windows x86 CLR, Node and Edge. An aggregate containing a managed reference retains a located diagnostic.

The unchanged WinForms package reaches the same `ComScope<IOleControlSite>` field address. Recognizing the helper is one prerequisite. Its callers also require a reference-free ref-struct layout, in-place construction in native frame storage and forwarding of that storage into the instance disposal method.
