# Stage 312 · Native integer unary operations

StandardWasm compiles the CLI `not` and `neg` instructions on native integers using the wasm32 word width. A complement result can carry nonzero bits even when its input was a known native zero.

```text
npm run demo -- stage-312-native-unary
```

The ordinary .NET Standard DLL compares signed and unsigned complements, zero and high-bit values, unchecked negation of the minimum word, and clearing a tag bit by combining complement and AND. The same fixture is tested in Windows x86 CLR, Node, and Edge.
