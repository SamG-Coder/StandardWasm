# Stage 346 · Multiplication in readonly byte-span indices

A straight-line index expression can multiply integer operands before reading a readonly byte span. The proof tracks the index operands above the span descriptor, and the normal verifier checks their stack types. The span still performs its usual bounds check. The compiler keeps CoreLib's original `Log2SoftwareFallback` and its lookup table.

```sh
npm run demo -- stage-346-multiplied-byte-span-indices --no-rebuild
```

Use the generated compiler or set `STANDARDWASM_COMPILER_PATH`. Windows x86 CLR, Node and Edge compare the product plus bias, wrapping multiplication, unsigned shift, and typed out-of-bounds case. The expected values are 191, 173, 239, and 607. The runner records hashes and versions under `artifacts/stage-346-multiplied-byte-span-indices/`.
