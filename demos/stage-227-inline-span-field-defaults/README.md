# Stage 227 · Defaulting inline span fields

Run `npm run demo -- stage-227-inline-span-field-defaults` with Windows x86
.NET 10, Node and Edge. The three runtimes compare the same ordinary
assembly. Evidence is written to `artifacts/stage-227-inline-span-field-defaults/`.

When an ordinary managed value owner exposes a writable span field to an
immediate, exact `initobj`, the compiler uses the same field replacement as
for existing inline defaults. The field becomes a default span and the
rest of the current owner stays intact. Aliases to that owner see its new
snapshot, while previously copied owners and backing arrays retain their
values.

The stage covers writable and read-only character spans and an Int32
read-only span. The original CoreLib interpolated string handler uses this
pattern to clear its character span before returning the pooled array.
