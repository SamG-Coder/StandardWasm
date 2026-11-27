# Stage 341 · Fixed-buffer copies

The managed type registry gives a verified CLR fixed buffer one storage slot for each element. The native aggregate copy uses its original element width and native stride. A snapshot includes the whole buffer when the source is a native pointer or an address-taken local.

The fixture registers an ordinary local, copies a record through native memory, and copies an address-taken local. The native memory case writes and checks 128 UTF-16 code units and the fields on both sides of the buffer. The address-taken case checks that a later source write leaves the copy intact.

```sh
npm run demo -- stage-341-mutable-fixed-buffer-copies --no-rebuild
```

Use the generated compiler or set `STANDARDWASM_COMPILER_PATH`. The same fixture runs on Windows x86 CLR, Node and Edge. The result file records the values, fixture hash, and compiler hash under `artifacts/stage-341-mutable-fixed-buffer-copies/`.
