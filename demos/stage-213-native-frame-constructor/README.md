# Stage 213 — In-place native frame constructors

A struct held in native frame storage can run a constructor whose managed
body assigns its native-word argument to exactly one instance field. The
compiler verifies the original signature and body, preserves evaluation of
the argument and writes directly to the proven field offset in the local's
existing frame slot. A pointer borrowed from the local then sees that write.

After building the compiler, run on Windows with x86 .NET and Edge.

```powershell
npm run demo -- stage-213-native-frame-constructor
```

The fixture compares pointer constants, null, a native-word argument and
separate owners against the original x86 CLR assembly. It also checks that
a borrowed pointer sees the same local reinitialized in place. A constructor with
an additional observable operation or a declaring type initializer produces a compiler diagnostic. Results
are written to `artifacts/stage-213-native-frame-constructor/results.json`.
