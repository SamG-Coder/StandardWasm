# 0083 · Native imports for blittable P/Invokes and InternalCalls

Status is proposed in #274 (Stage 156), the first milestone of that issue. Codex agreed the
import interface (handoff codex-274-import-interface-ack). The executable loader keeps the fixed
Win32 and typed native import namespaces separate and shares the two last-error slots within each
instance. This builds on decision 0024, where the managed/native boundary stopped at the compiler.

## Problem

A method without IL, such as a DllImport P/Invoke, a CoreLib QCall or an `InternalCall`, was
-2 when reached. The Stage 117 census counts 773 distinct reached native entry points in the
unchanged WinForms path. This step adds the seam and its first entries. It is not the full
surface.

## Decision

A reached **static** method that is a P/Invoke (PinvokeImpl with an ImplMap row, QCalls
included) or an `InternalCall` compiles to a **thunk**. The thunk is an ordinary defined function
whose body is synthetic, pre-linked records (`NativeThunks`):

```
NativePresent   present(k) -> i32          does the host implement entry k?
brtrue          call
NativePresent   library(k) -> i32          is entry k's library available?
brtrue          entry-missing
ldstr; newobj DllNotFoundException(string); throw           the CLR's message
entry-missing:
ldstr; newobj EntryPointNotFoundException(string); throw    the CLR's message
call:
ldarg 0..n-1
[NativeHelper clearLastError]               SetLastError = true only
CallNative      the typed import
[NativeHelper saveLastError]
ret
```

- **Call sites are unchanged.** They call the thunk like any method, and the type-initializer
  prologue of a precise type still runs before the import, as on the CLR.
- **One import per entry**, each with its own function type, keyed by:
  - P/Invoke and QCall: module `native:<ModuleRef name>`, name = the ImplMap entry point.
  - InternalCall: module `internalcall`, name = `Namespace.Type::Method`.
  - Host helpers: module `standardwasm:native` (`present`, `library`, `clearLastError`, `saveLastError`).
- **Table placement.** The entries form one table after every fixed import group. No feature bit
  is added: #232 owns bit 16 and up, and the table's presence comes from its count. Earlier
  groups keep their function indices. Discovery adds entries during compilation, and
  `CompileProgram` then recompiles with the grown table reserved. This is the existing
  host-feature re-emit loop.
- **Missing libraries and entries.** The CLR binds lazily and raises at the call, with two
  different types:
  - A library that cannot be loaded raises `DllNotFoundException`: "Unable to load DLL 'M' or one
    of its dependencies: The specified module could not be found. (0x8007007E)".
  - A library that loads but lacks the entry raises `EntryPointNotFoundException`: "Unable to find
    an entry point named 'E' in DLL 'M'."

  The host answers `present(k)` from the entries it implements, and `library(k)` from whether it
  implements any entry of that DLL. InternalCalls belong to the runtime, which is always
  available. The module then raises the same CoreLib exception with the CLR's message, so the
  same `catch` clause runs as on the CLR (Codex's review found that the two cases had been
  conflated). A missing entry is never a silent stub or a raw trap. Without bindable CoreLib
  `(string)` constructors for both types, the thunk is a -2 diagnostic. The message text is the
  CLR's on Windows; it names the Windows loader error.
- **Last error.** `SetLastError = true` follows the CLR stub: the host clears the system error
  before the call and saves it as the P/Invoke error after it. `Marshal.GetLastPInvokeError` and
  `SetLastPInvokeError` are CoreLib InternalCalls that the host registry answers from that state,
  and `Marshal.GetLastWin32Error` is ordinary IL over them.

## Signatures (step 1)

Only blittable scalars are lowered. The method signature blob is read directly, not the
compiler's stack types:

| Element type | Wasm |
| --- | --- |
| `int8` … `uint32` | i32 (narrow results normalized to their CLI width) |
| `int64`, `uint64` | i64 |
| `float32`, `float64` | f32, f64 |
| `native int`, `native uint` | i32 (wasm32) |
| `void` (result) | none |

These stay a -2 at the native method (`location` names its MethodDef):

- `bool` and `char`, because DllImport marshals them (4-byte Win32 BOOL, ANSI char by default)
- strings, arrays, pointers, structs and delegates
- non-PreserveSig imports (HRESULT translation)
- instance methods, generics and varargs

Pointer and struct marshalling need decision 0025's linear-memory model (#275, #132) and come in a
later step.

## Host registry

`scripts/native-imports.mjs` builds the import object from a module's own import list:

- `nativeEntries(module)` lists the required entries in table order.
- `nativeImports(module, registry, state)` binds the registry and the helpers.

Entries are keyed `"<dll>!<entry>"` or `"internalcall!<Namespace.Type::Method>"`. The first registry,
`windowsScalars`, implements the x86 `kernel32` and `ucrtbase` scalar entries Stage 156 uses, with
their C semantics:
- `MulDiv`: a 64-bit product, rounded half away from zero, -1 on overflow or a zero divisor
- `fmaxf`: ignores NaN
- `roundf`: rounds half away from zero
- `copysignf`: keeps the sign of -0

It also implements the last-error InternalCalls. The registry is code, so its claims are
checked against the CLR running the real DLLs, not assumed.

## Limitations

- ExactSpelling probing (the CLR's `A`/`W` suffix search) is not modeled. The registry key is the
  declared entry point.
- Calling conventions are irrelevant in Wasm and are ignored. Every scalar import has one Wasm
  signature.
- `copysignf` cannot see the sign of a NaN from JavaScript.

## Verification

`npm run demo -- stage-156-native-imports` runs an ordinary .NET Standard 2.0 fixture on the x86
CLR, against the real `kernel32` and `ucrtbase`, and in Node and Edge with the registry. It covers:
- `MulDiv` rounding and boundaries, `abs(int.MinValue)`, `llabs`, `sqrt`, `fmaxf` with NaN,
  `roundf(-2.5)`, `copysignf(-0)`, and SetLastError plus `GetLastWin32Error`
- a missing kernel32 entry caught as `EntryPointNotFoundException` and an absent library caught as
  `DllNotFoundException` (17 values in all), each by its own handler in a fixture that catches both
- one located -2 for a `string` argument
