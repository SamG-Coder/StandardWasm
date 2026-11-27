# Stage 351 · Canonical RuntimeType casts

An x86 CLR oracle exercises the real internal `System.RuntimeType` with `isinst` and `castclass`. The verifier embeds the product's cast emitter in a small Wasm module and compares results in Node and Edge. It covers multiple type descriptors, nulls, Module and Assembly descriptors, and an object with the same Wasm storage shape as a type descriptor.

```sh
npm run demo -- stage-351-runtime-type-casts --no-rebuild
```

The dynamic cast checks identity against the linked type objects. Already proven descriptor operands can retain the reference directly, while an unsuccessful `castclass` throws the managed invalid-cast tag. The Stage 350 comparer fixture separately exercises linkage against the unmodified CoreCLR bodies. Generic activation itself remains a compiler checkpoint.
