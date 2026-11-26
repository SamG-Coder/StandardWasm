# Stage 138 · Inline-array layouts

Run `npm run demo -- stage-138-inline-array-layouts`. The .NET 10 fixture copies a three-element inline array, overwrites the original's declared field, and returns 30 from the independent values. The CLR comparison suite also exercises default initialization, value arguments, signed boundaries and a closed generic automatic-layout inline array of object references.

The compiler reads the platform `InlineArrayAttribute` and reserves one managed slot per element. See [decision 0066](../../docs/decisions/0066-inline-array-layouts.md) for the storage model and remaining work on managed references.
