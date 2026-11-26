# Stage 56 · Typed null failures in instance calls

Run `npm run demo -- stage-56-instance-null`. Calling
`CallCleanup(0, 2)` returns `-101`. The argument updates the state,
the callee's receiver check raises `NullReferenceException`, and the
caller's `finally` adds another 100 before the catch runs.

The instance method body ignores its receiver, so the exception comes
from `callvirt` itself. The stage also exercises the event-field address
check during `Interlocked.CompareExchange`, and verifies that a valid
exchange still replaces the callback.

Run `npm test` for CLR, Node and Edge comparisons and generated-compiler
replay. The instance calls use known same-module targets on sealed classes.
General virtual dispatch and general managed field addresses remain open.
