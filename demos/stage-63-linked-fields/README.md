# Stage 63 · Instance fields across DLLs

Run `npm run demo -- stage-63-linked-fields`. Calling `Run(5)` returns `33`.
Two .NET Standard dependencies each define `FieldData.Cell` with a
different layout. The caller reads and writes fields through both
layouts, including a recursive class reference and a primitive array.

The CLR, Node and Edge suite also covers a readonly field read,
typed null failures, dependency order, and a mismatched field signature.
