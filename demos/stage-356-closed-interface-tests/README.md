# Stage 356 · Closed generic interface casts

The compiler can test a widened reference against a closed CoreLib interface when membership is exact for that construction. Invariant slots, value-type arguments and a contravariant `Object` slot cannot admit a different type argument. The compiler compares the linked interface implementations and the canonical runtime representations for boxed values and strings. It retains the existing proven-stack path for conversions that need general variance or the CLR's special array interface rules.

```sh
npm run demo -- stage-356-closed-interface-tests --no-rebuild
```

Windows x86 CLR, Node and Edge compare identity, null, rejection of the wrong generic construction, managed `InvalidCastException`, primitive and enum boxes, strings, and dispatch to the original explicit interface method. Both unchanged WinForms linker replays pass the `IEqualityComparer<object>` cast in the original `Dictionary.Resize`.
