# 0058 · Managed fieldless sequential values

A sequential value type can specify a one-byte nominal size with default
packing even when it declares no instance fields. The CLR uses this shape
for `System.Threading.Tasks.VoidTaskResult`. Retain the declaring type's
managed identity and use the existing empty GC aggregate for values and
their copies. Static fields belong to the declaring type and do not change
the aggregate. Raw layout observers still require their own lowering.
Keep custom packing and nonempty declared sizes outside this rule.

The original async entry computes a field value with a direct static call
while the destination struct address is already on the IL stack. Value
address lowering uses the call signature's stack effect to tell whether a
direct static call consumes only operands above the pending address. The
normal linker still binds and validates the call. The address must end in
one of the existing supported consumers, with no branch or exception-region
entry between producers and consumers. Calls that take the location by
reference still require the managed address work in #273.
