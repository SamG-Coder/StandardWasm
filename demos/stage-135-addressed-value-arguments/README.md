# Stage 135 · Addressed value arguments

Compile ordinary .NET Standard methods that take the address of a
by-value struct argument. The fixture calls a read-only instance method,
updates the argument's own field and clears the argument. The caller's
copy keeps its original value.

Build the portable compiler and run
`npm run demo -- stage-135-addressed-value-arguments`. Node and Edge
compare nine calls against the CLR using the same DLL. The field-address
receiver in the original WinForms constructor remains a separate part
of the managed-address work.
