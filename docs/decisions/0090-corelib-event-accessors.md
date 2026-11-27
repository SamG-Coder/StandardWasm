# 0090 · CoreLib's own EventHandler accessors

Status is proposed in #256 (Stage 181). Codex reserved the numbers (handoff
codex-256-269-parallel-lanes). This builds on the #232 integration line, which already binds the
accessor's signature and CoreLib's `EventHandler` storage, and on Stage 119's EventHandler
composition records and decision 0088's open static targets.

## Problem

The unchanged WinForms `NativeWindow..cctor` subscribes a static handler to
`AppDomain.CurrentDomain.ProcessExit`. CoreLib implements that event's add and remove accessors as
the standard field-like event loop:

```
ldarg.0; ldfld ProcessExit; stloc.0
loop: ldloc.0; stloc.1; ldloc.1; ldarg.1
      call      System.Delegate::Combine(Delegate, Delegate)        (MethodDef, same image)
      castclass System.EventHandler                                   (TypeDef)
      stloc.2; ldarg.0; ldflda ProcessExit; ldloc.2; ldloc.1
      call      Interlocked::CompareExchange<EventHandler>(ref, , )   (MethodSpec over a MethodDef)
      stloc.0; ldloc.0; ldloc.1; bne.un loop; ret
```

Stage 119 recognizes `Delegate.Combine/Remove` and `CompareExchange<EventHandler>` only as
cross-assembly **MemberRefs**. Inside CoreLib they are MethodDefs, so the call fell into the original
`Delegate.Combine` body. Its virtual `CombineImpl` was -1 at `[CoreLib, MethodDef 1204, IL 0x07]`,
the WinForms boundary.

## Decision

- **Exact identification** (`CoreLibDelegateDefinitions`, in AssemblyReader). Each definition is
  recognized only in the image that is the CoreLib definition, by declaring TypeDef, name and exact
  signature:
  - `System.Delegate::Combine` / `::Remove` with signature `(Delegate, Delegate) -> Delegate`
  - the `System.EventHandler` TypeDef
  - a MethodSpec of `System.Threading.Interlocked::CompareExchange<T>(ref T, T, T)` at `EventHandler`
- **Composition.** A `Combine`/`Remove` MethodDef call lowers to the existing CombineHandler or
  RemoveHandler record **only when the next instruction is `castclass System.EventHandler`** and no
  branch, switch or handler boundary enters that cast. A Combine of any other delegate type keeps the
  original body. Those records build EventHandler invocation lists, so they must never type another
  delegate.
- **Exchange.** The compare-exchange joins the existing fused `ExchangeHandlerField` path, exactly as
  for the MemberRef form.
- **The accessor itself is unchanged.** Its loop, the retry comparison and the field writes run as
  written, so there's no substitute accessor or status-only shortcut.

## Semantics

Combining with null, removing a handler that was never added, and repeated add/remove pairs follow
the EventHandler records' existing CLR semantics (Stage 119). The handlers are only invoked at
process exit, which a browser module never reaches. Stage 181 therefore checks that subscription
compiles and runs, and that the same module's library events still compose correctly. Invocation
order at exit is out of scope.

## Verification

`npm run demo -- stage-181-corelib-event-accessors` compares six kernels of an ordinary .NET
Standard 2.0 fixture with the x86 CLR in Node and Edge. They cover static and instance handlers,
repeated pairs, removing a handler never added, a null handler, and the CoreLib accessor beside a
library event.

The Stage 117 census of the unchanged WinForms application now stops at
`[System.Windows.Forms, MethodDef 18858, IL 0x0047]` (a reference `newobj`), past `[6,1204,7]`.

## Not yet

Delegate types other than `EventHandler` reached through CoreLib's own `Combine` (for example
`UnhandledExceptionEventHandler`) still reach the original body. General delegate composition is the
next #256 milestone.
