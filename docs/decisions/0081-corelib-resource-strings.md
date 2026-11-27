# 0081 · CoreLib resource strings at compile time

Status is proposed in #304 (Stage 154). Claude took it at Codex's request (handoff
codex-302-and-170-response). It unblocks #170.

## Problem

CoreLib's generated `SR` class has one property per resource key:

```
internal static string X => GetResourceString("X");   // ldstr "X"; call SR.GetResourceString; ret
```

`GetResourceString` reaches `InternalGetResourceString` and `ResourceManager`. Those use culture
fallback, reflection and locking, which the compiler cannot build. The compiler compiles whole
methods, as the CLR's JIT does, so any CoreLib body that names an `SR` property was rejected (-2
at `SR.InternalGetResourceString`), even on a branch that never runs. Examples are every
parameterless exception constructor and `TypeInitializationException`'s
`message ?? SR.TypeInitialization_Default`.

## Decision

While linking a CoreLib body, the compiler folds a call to the **verified** CoreLib method
`System.SR::GetResourceString(string)` into a string literal. The method is identified by its
declaring TypeDef `System.SR`, its name and the signature `00 01 0E 0E`, in the image that is the
CoreLib definition, and the fold applies only when the argument is the literal `ldstr` that
immediately precedes the call:

- The key is looked up in CoreLib's own embedded `System.Private.CoreLib.Strings.resources`.
  `ResourceStrings` parses the `.resources` binary format, version 2 (magic `0xBEEFCACE`):
  the name section in UTF-16LE, and string values in 7-bit-length-prefixed UTF-8.
- The `ldstr` record takes the resolved value, and the call becomes a no-op. The stack
  effect is unchanged: [String] in and [String] out.
- If the key is absent, or not a string resource, the call is left in place, and the existing
  diagnostic stays.

## Semantics

- **Culture.** These are the neutral strings, which the CLR returns when no satellite resource
  assembly applies. That is the case for an invariant or English UI culture, which is the
  browser host's culture. A non-English UI culture would load a satellite assembly on the CLR;
  that is out of scope.
- **Resource keys.** `UseSystemResourceKeys` (the trimming switch that makes `SR` return keys)
  is off by default on the CLR, and the folded value matches that default.
- **Scope.** Only literal keys are folded. A computed key, such as `SR.GetResourceString(name)`
  with a variable, still needs `ResourceManager` and stays diagnosed. `SR.Format` with arguments
  is ordinary code over the folded format string.

## Verification

`npm run demo -- stage-154-corelib-resource-strings` compares eight default-message exception
kernels with the x86 CLR in Node and Edge. With #170, `TypeInitializationException`'s upstream
constructor compiles unchanged.
