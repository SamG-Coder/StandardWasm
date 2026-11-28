# Stage 305 · Native addresses of immutable PE data

The compiler follows a closed static interface call to a readonly getter that views a fully backed PE `FieldRVA` range. An exact native address conversion of that range uses a stable address for its field in the module's linear memory. The match verifies the element type, native width, alignment and absence of initializer side effects.

```text
npm run demo -- stage-305-static-rva-pointers
```

The same DLL reports the original bytes, stable addresses across separate callers, and distinct addresses for different fields on Windows x86 CLR, Node, and Edge. A conversion of an ordinary managed static field still produces a located compiler diagnostic. The supported views read immutable payloads such as native interface identifiers.
