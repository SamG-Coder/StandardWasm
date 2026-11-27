# Stage 177 · String copy to span

Compile the original CoreLib `String.CopyTo(Span<char>)` from calls in an
ordinary .NET 10 library. The compiler preserves the original length check
and failure path and translates the verified character move to tracked spans.

Run `npm run demo -- stage-177-string-copy-to-span`. The Windows x86 CLR,
Node and Edge agree on UTF-16 code units and untouched sentinels around a
slice, stack-backed destinations, short-destination failure, empty copies,
and null source failure.
