# 0063 · Lower nonescaping value argument addresses

## Decision

A by-value argument has its own frame slot. The short and long
`ldarga` forms use the existing frame-address path for structs, so a
local store changes only the callee's copy. The address analysis must
prove the use fits the supported operations and stays inside the frame.
Taking the address of a value-type instance method's `this` argument
slot is a different operation because the slot already contains a
managed address.

## WinForms field receivers

The original Form constructor takes the managed address of
`Form._formStateEx`, evaluates the other arguments, and then calls
`BitVector32.set_Item(Section, int)`. The upstream setter itself takes
the address of the by-value Section argument in order to read its
properties. Stage 135 covers that argument operation and verifies the
original Section accessors separately.

A field address needs to represent the actual storage location through
the call. Writes must be visible to aliases, and completed writes must
survive an exception that leaves the method. The parent reference must
remain live, its null check must precede the subsequent arguments, and
the value must be read from the current location when the method uses
it. The current argument lowering needs no general managed address and
does not change the field-address path.
