# 0045 · Sequential reference layouts

Status: accepted for Stage 115 (#220).

The browser-wasm CoreLib from the .NET 10.0.10 Mono runtime pack declares 41
reference types with `SequentialLayout`, among them `System.Delegate`,
`System.Type`, `System.Exception` and `System.Threading.Thread`. None of them
has a `ClassLayout` packing or size. The desktop CoreCLR CoreLib declares
`Delegate` and `Type` as auto layout. An original Blazor publish therefore
reaches sequential classes as soon as it reads a `Task` field.

## Decision

A class TypeDef with `SequentialLayout` (`0x08`) lowers to the same nominal
Wasm GC struct as an auto class. It keeps the same base chain, the same
declaration-ordered fields and the same type-initializer rules. Managed field
access is by FieldDef, so it cannot observe the CLR byte order. Byte-level
views are interop marshalling, `Marshal.SizeOf`, `Unsafe.As` overlays and raw
field addresses. They stay diagnosed where they occur, so sequential order is
never silently dropped.

The compiler continues to diagnose these shapes:

| Shape | Reason |
| --- | --- |
| `ExplicitLayout` class | Field offsets can overlap object references. |
| Sequential class with `Pack` or `Size` | The CLR object extent changes and no byte view preserves it. |
| Sequential class over an auto class other than `Object`, outside the platform CoreLib | CoreCLR throws `TypeLoadException` when it loads such a type. |

A sequential class with a precise type initializer was diagnosed here until
Stage 129. It now runs its initializer as an auto class does (decision 0057).

The CoreLib exception covers the runtime's own types. The runtime that ships a
CoreLib loads that CoreLib's types: Mono's `System.Type` is sequential over
an auto `MemberInfo`, and 29 of the 41 sequential CoreLib classes have a base
without layout. Application and library DLLs follow the CoreCLR rule because
that rule is what the Windows baseline observes. A test run under the Mono
runtime could later show that Mono also loads the shape for ordinary DLLs.
Admitting it then needs its own differential case.
