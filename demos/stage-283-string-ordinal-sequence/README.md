# Stage 283 · CoreLib ordinal string comparison

Run an ordinary .NET Standard library through the linked CoreLib string inequality operator. Its original managed reference, null and length checks remain in place. The compiler verifies the private two-string byte comparison before comparing the same UTF-16 code units in tracked Wasm storage.

```text
npm run demo -- stage-283-string-ordinal-sequence
```

The expected outcomes are 101, 103, 107, 109, 113, 127 and 131. The fixture covers equal content in distinct string objects, both bytes of a UTF-16 code unit, embedded NUL, unpaired surrogates, null, empty and unequal-length cases, and an ordinary user method named `EqualsHelper`. Set `STANDARDWASM_MONO_RUNTIME` to the Mono runtime pack's `runtimes/browser-wasm` directory to verify the same assembly against matching Mono dependencies.

The verified comparison keeps both raw addresses inside CoreLib's private same-length comparison. General string-backed byte addresses, copies and hashing are outside this stage.
