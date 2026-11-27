# 0082 · Bound catch variables

Status is proposed in #269 (Stage 155). Codex reserved the stage and decision numbers. This builds
on decision 0054 (managed exception objects) and #170 (decision 0057, typed initializer failure).

## Problem

Decision 0054's dispatcher resumes a catch handler after its leading `pop`, so a handler had to
discard the exception. `catch (X e)` that reads `e` starts with `stloc` (or uses the value
directly), and `ExceptionClauses.Validate` rejected it with -2 at the handler offset, for every
class. That blocked ordinary code such as `catch (Exception e) { return e.HResult; }` and made
exception state (`InnerException`, identity, `HResult`) unobservable from managed code.

## Decision

A catch handler that does not start with `pop` is **bound**. Its clause's resume offset is the
handler's first instruction, and the handler starts with one stack value:

- **Stack typing.** `StackDepths` seeds a bound handler at depth 1, typed as the clause's caught
  class, or `Object` for a catch of `System.Object`. Validate still requires that no edge other
  than the dispatcher enters a handler start.
- **A thrown managed object** (decision 0054's object tag) is already in the dispatcher's scratch
  `eqref`. Before entering the handler, the dispatcher casts it to the caught class (it has
  passed the clause's `ref.test`) and writes it into the handler's depth-0 spill local.
- **A fixed tag** carries no object. For the four tags the CLR raises with its parameterless
  constructor's message (Overflow, DivideByZero, IndexOutOfRange, NullReference), the dispatcher
  builds that CoreLib exception, allocated as `newobj` allocates (`struct.new_default`, then the
  constructor). From then on the exception *is* that object: with a rethrow in scope the clause
  saves it as the caught object and clears the fixed `exnref`, so `throw;` raises it on the
  object tag and an outer handler sees the same reference, as on the CLR.
- **InvalidCast and the argument tags** reaching a bound handler stop with `unreachable`. The CLR
  message for a failed cast names both types, and the argument checks name their parameter. The
  raising sites don't supply those details, so a default-constructed object would have a
  different `Message`. This is an explicit stop, not an approximation.

## Diagnostics

These stay located -2 diagnostics at the handler offset:

- A bound clause whose class the bundle cannot lay out (no object tag), for example without CoreLib.
- A bound clause that can receive one of the four default-message tags while CoreLib's
  parameterless constructor cannot be bound.

Interface and value-type catch types keep decision 0054's rules.

## Semantics

- **Identity.** A thrown object reaches the handler unchanged. A fixed-tag exception gets one object
  at the first bound handler, kept through rethrow. An unbound (`pop`) handler of a fixed tag still
  rethrows the fixed tag, as before, so a later bound handler builds its object then.
- **State.** The built object has its constructor's state. Its `HResult` matches the CLR, for
  example `COR_E_DIVIDEBYZERO` 0x80020012.
- **Message.** `Exception.get_Message` compiles only once a string can be passed as an `object`
  (`SR.Format(string, object)` inside it). That is #232's Stage 133 work and is not part of this
  decision.

## Verification

`npm run demo -- stage-155-bound-catch-variables` compares 15 kernels with the x86 CLR in Node and
Edge. They cover:

- a caught object and its `HResult`
- a library exception class and a base-class catch
- `InnerException`
- the four fixed tags caught as `Exception`
- rethrow identity for a fixed tag and an object
- nested bound handlers
- the #170 `TypeInitializationException` identity and inner exception
- the explicit stop for a runtime cast failure
