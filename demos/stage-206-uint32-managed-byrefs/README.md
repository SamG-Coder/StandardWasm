# Stage 206 · UInt32 managed byrefs

Run `npm run demo -- stage-206-uint32-managed-byrefs` with a built StandardWasm compiler. Set `STANDARDWASM_COMPILER_PATH` to select a specific compiler artifact.

The same ordinary .NET Standard library runs on the x86 CLR and compiles in Node and Edge. The runner compares five inputs through six methods and covers direct and closed-generic `ref` and `out`, logical unsigned shifts, two aliases to one local, and separate `int` and `uint` overloads. The inputs include bit 31 and `uint.MaxValue`.

[ECMA-335 I.8.7.2](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf) reduces `uint32` and `int32` to the same verification type and gives their managed pointers the same verification type. The compiler therefore reuses its existing four-byte location. CLI signature tokens and closed generic argument identities retain their original types, and each IL operation selects signed or unsigned interpretation where needed.
