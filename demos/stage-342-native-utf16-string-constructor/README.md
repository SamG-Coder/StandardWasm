# Stage 342 · Native UTF-16 string construction

`new string(char*)` resolves the genuine CoreLib internal constructor and links the original managed `String.Ctor(char*)` helper. Its null and zero-length branches keep using the canonical empty string. The verified UTF-16 scan and copy into a fresh string use Wasm linear memory and the managed character vector.

The fixture checks null and empty input, unaligned input, a terminating NUL followed by unrelated data, surrogate code units, and copies that retain their contents after the native buffer changes. Invalid native addresses remain subject to Wasm memory bounds.

```sh
npm run demo -- stage-342-native-utf16-string-constructor --no-rebuild
```

Use the generated compiler or set `STANDARDWASM_COMPILER_PATH`. The four cases should return `1` on Windows x86 CLR, Node and Edge. The runner records exact hashes and versions in `artifacts/stage-342-native-utf16-string-constructor/results.json`.
