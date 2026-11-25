# Stage 02 · Managed arrays

Run `npm run demo -- stage-02-managed-arrays` from the root after `npm ci`. Expected output is `PrefixChecksum(2, 3) = 8`. Sequences.cs allocates a zero-initialized managed int array, writes 4 into its second element, then weights that element by 2. The compiler translates allocation and element access to Wasm GC array instructions.
