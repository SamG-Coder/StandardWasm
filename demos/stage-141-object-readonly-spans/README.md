# Stage 141 · Read-only object spans

Run `npm run demo -- stage-141-object-readonly-spans`. An ordinary .NET 10 library creates a `ReadOnlySpan<object>` over an inline array. Resetting the array is visible through the span and the demo returns 17.

The CLR comparison includes live updates across managed calls, default span length, and typed bounds failures for default and nonempty spans. See [decision 0069](../../docs/decisions/0069-readonly-object-span.md).
