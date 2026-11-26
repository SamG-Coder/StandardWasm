# Stage 144 · Writable character spans

Run `npm run demo -- stage-144-writable-character-spans`. The .NET 10 fixture writes through a `ref char` obtained from `Span<char>`. The array, original span and read-only span observe the same code unit.

The CLR comparison covers packed UTF-16, empty/default views, typed bounds and evaluation order, and writes that survive exceptional unwinding. See [decision 0072](../../docs/decisions/0072-writable-character-span.md).
