# Stage 343 · Reads through fixed-buffer field addresses

An immediate `ldind.u2` through a verified `char` or `ushort` value field uses the existing field load. The compiler verifies that control flow cannot enter between the address and its read. The rule applies to a managed aggregate or to a native pointer with known layout. Address conversions and escaping references keep their existing handling.

The fixture checks a default fixed buffer, a managed snapshot after native memory changes, a direct native receiver, and an unsigned field read. It exercises the read of the first UTF-16 code unit used by CoreCLR's original Windows version query.

```sh
npm run demo -- stage-343-fixed-buffer-field-reads --no-rebuild
```

Use the generated compiler or set `STANDARDWASM_COMPILER_PATH`. Windows x86 CLR, Node and Edge should return 0, 65572, 33059, and 54321 respectively for the four cases. Evidence is written to `artifacts/stage-343-fixed-buffer-field-reads/results.json`.
