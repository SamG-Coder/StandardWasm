# Stage 48 · Managed overflow handlers

Run `npm run demo -- stage-48-overflow-catch`. The generated `Increment(int.MaxValue)` returns `-1` from its C# catch block.

The compiler supports a single `catch (OverflowException)` that discards the exception object. It validates the handler and protected instruction boundaries, checks CIL control flow and `leave`, and uses Wasm `try_table` to dispatch the matching tag. Checked callees and signed divide and remainder overflow use the same tag. Ordinary divide-by-zero still traps. An overflow before the protected region or raised by the handler propagates to the caller.

`npm test` compares the same .NET Standard DLL in the CLR, Node, and Edge. Catch objects, multiple clauses, other exception types, filters, and finally blocks remain open.
