# 0054 · Throw and catch managed exception objects

Status: proposed (#269). Builds on decisions 0012, 0013 and 0015. The interface was agreed with Codex
in the local handoff before implementation.

## Context

Exceptions used six payload-free Wasm tags (overflow, divide by zero, index, null, invalid cast and
argument). A catch clause stored a bit mask over them. Any other catch type, and any `throw` of a
real object, returned -2. In the Stage 117 census that was the first blocker for 2,175 reached
WinForms methods. The largest site is CoreLib's `SR.GetResourceString`, which catches
`MissingManifestResourceException`, a CoreLib TypeDef the old reader could not name.

## Decision

- **Object tag.** A thrown managed object travels on tag 6, typed `(eqref) -> ()`. That is the
  compiler's representation of `System.Object`. The tag is exported as `\0object` so the host can
  recognize an uncaught managed object. Tags 0–5, their `catch_ref`/`throw_ref` paths and the
  `throw null` behavior are unchanged.
- **Plain catch for the object tag.** The object tag is caught with a plain `catch` into an outermost
  `block (result eqref)`, not with `catch_ref`. That needs no multi-value block type, whose index is
  only known at module-write time, after the method bodies are emitted.
- **Clause masks.** A catch type that resolves to a linked class adds `ExceptionTypes.Object` and the
  class above `ExceptionTypes.ClassShift`. `System.Object` means any object. Inside the authentic
  CoreLib (`IsCoreLibDefinition`), the TypeDefs `System.Exception`, `SystemException`,
  `ArgumentException` and the other fixed-tag types also keep their fixed bits. So
  `catch (Exception)` still receives implicitly raised CLR exceptions such as NullReferenceException.
  A same-named user TypeDef never gets fixed bits.
- **Nominal matching.** Object dispatch tests clauses in order with `ref.test` against the clause
  class's Wasm GC type. Wasm subtyping mirrors the bundle's class hierarchy, so a derived exception
  matches its base, derived-before-base ordering holds, and same-named classes from different
  assemblies stay distinct.
- **Throw.** `throw` of a managed class reference tests it for null first. A null reference raises
  NullReferenceException, as on the CLR, even when its static type is an exception class. A non-null
  reference raises tag 6 with that reference. Interfaces and value types stay diagnosed.
- **Rethrow and finally.** The object path keeps a parallel set of `eqref` locals: a scratch local and
  one saved object per clause. Entering a handler through either kind clears the other kind's saved
  reference, so repeated visits cannot rethrow a stale exception. `throw;` and the end of a `finally`
  re-raise whichever is present: `throw_ref` for a fixed `exnref`, or `throw 6` with the original
  reference for an object.
- **Finally cannot be skipped.** Every method with exception regions declares and catches tag 6, so an
  object exception always passes through enclosing `finally` blocks. Bodies without regions propagate
  objects natively.
- **Handler entry.** A handler must still begin with `pop`; a handler that binds the exception variable
  stays -2. A fixed exception carries no object, so this rule keeps any handler from observing a
  missing object.
- **Filters.** Filters keep their existing -2 diagnostic at the handler offset.

## Verification

Stage 126 builds an ordinary .NET 10 library whose exceptions derive from the authentic
`System.Exception`. Each bundle carries the 32-bit `System.Runtime` and `System.Private.CoreLib`
(decision 0044), and the x86 CLR running the same DLL is the oracle. The self-hosted compiler matches
it in Node and Edge for:

- a typed catch
- derived-before-base ordering
- a base catch receiving a derived exception
- a mismatch reaching an outer handler
- `catch (Exception)`
- rethrow preserving the type
- finally ordering
- a null variable of the exception class type
- a repeated handler visit receiving a fixed exception and then an object
- same-named classes in two assemblies

A filter returns the explicit diagnostic, and an uncaught object reaches the host on the object tag.
