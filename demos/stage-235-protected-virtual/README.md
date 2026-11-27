# Stage 235 · Protected virtual methods on a closed declaring class

A generic class can call its own protected virtual method through an ordinary
closed MemberRef. The compiler resolves that declaration against the same closed
caller type and uses its existing virtual receiver and dispatch checks. The
receiver can be a compatible derived object, whose override is selected normally.
A null receiver raises the normal typed exception. Protected calls issued by a
different declaring class still require a separate receiver-access proof.

The same .NET Standard assembly runs on x86 Windows CLR, Node, and Edge. The
fixture checks the original base body, a derived override, base and derived
objects passed through the same method parameter, and a caught null exception.
The final case records the native result and verifies the located diagnostic for
a protected call from a derived declaring class.

Run the stage using the generated compiler.

```sh
npm run demo -- stage-235-protected-virtual
```

Results are written to `artifacts/stage-235-protected-virtual/results.json`.
