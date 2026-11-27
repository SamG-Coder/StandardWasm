# Stage 212 — Native frame storage for reference-free ref structs

A verified field-address helper can borrow a native field from a sequential
ref struct stored in its caller's frame. The compiler measures that frame
value from CLI field signatures, checks every field and allocates a single
native stack slot. The helper returns the field's offset within that slot.

After building the compiler, run on Windows with x86 .NET and Edge.

```powershell
npm run demo -- stage-212-scoped-native-frame-layout
```

Windows x86, Node and Edge agree on writing and clearing a field through
the borrowed pointer, independent owners, and replacing the original value
while an existing pointer still refers to its frame slot. The Node and Edge
compiler diagnostics also agree for an owner with a managed object field and
an owner with a managed byref field.

The scoped layout option applies to verified field addresses within a
native frame. Results are written to
`artifacts/stage-212-scoped-native-frame-layout/results.json`.
