# Stage 257 managed 64-bit references

The .NET Standard fixture passes signed and unsigned 64-bit managed references between ordinary compiled methods. The mixed class case uses a mutable `ulong`-backed enum, an ordinary object reference field, and an `int` field in one call. Other cases cover local slots, projected value fields, reads and writes through live aliases, closed-generic load, store and initialization, and null receivers. The runner uses the Windows x86 CLR as the oracle and compares the same compiled DLL in Node and Microsoft Edge.

Run `node demos/stage-257-wide-managed-byrefs/verify.mjs` with a built compiler. Set `STANDARDWASM_COMPILER_PATH` to select a frozen compiler artifact.
