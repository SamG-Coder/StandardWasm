# Stage 61 · Constructors and instance calls across DLLs

Run `npm run demo -- stage-61-linked-instances`. Calling `Run(5, 3)` returns
`1110`. The caller constructs and updates a `Counter` from a separately
built .NET Standard DLL. Its signed and unsigned overloads exercise exact
signature matching, and the returned reference preserves receiver identity.

The CLR, Node and Edge suite also compares checked constructor failures,
null receiver exceptions, and argument evaluation before the receiver
check. The stage covers public constructors and known instance bodies
on supported sealed classes. Class virtual calls also preserve the original
managed null exception. See [Stage 122](../stage-122-runtime-class-dispatch/README.md)
for runtime override selection.
