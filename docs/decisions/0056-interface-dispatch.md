# 0056 · Bind nongeneric interface slots

Status is proposed in #272.

A Windows Forms resource stream is read through an upstream managed wrapper.
The cleanup calls `IDisposable.Dispose` and must select the inherited
`Stream.Dispose` implementation. That body dispatches `Dispose(bool)` to
the protected `UnmanagedMemoryStream` override. Both selections must follow
the original CLR slot semantics.

Use the Stage 122 allocation fixed point for interface calls on managed class
references. Match each candidate by its nominal implemented interface. Preserve
the typed `callvirt` null check and the restrictions on managed references
crossing the export boundary. A matching body may narrow its receiver, while
its other parameter and return types must agree with the declaration.

Walk the receiver's class chain. Read explicit MethodImpl records by the
declaration's assembly and method identity, including references through
a facade. Match local and bundled declarations. Consider name-based implicit
implementations from the closest class that declares the interface map. A
subclass's public method can hide a name without changing the inherited map.
Dispatch an overridable class body through its class slot. A final body
already supplies its concrete target. For class virtual dispatch, a same-name
MethodImpl can affect the slot only if its declaration belongs to the class
hierarchy. Unsupported class mappings still produce a diagnostic.

This stage supports local final instance-virtual MethodImpl bodies. Generic
interface dispatch, default interface method selection, value-type receivers,
and other explicit body forms still produce located diagnostics when they
are required. Preserve those diagnostics until the compiler can reproduce
their CLR semantics.

The Stage 128 fixture checks a dependency-defined slot and a same-name local
slot, inherited mappings, explicit implementations, hidden members, remapping,
and a typed null receiver. The original WinForms startup replay supplies the
upstream integration check after #280.
