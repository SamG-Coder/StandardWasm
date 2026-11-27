# Stage 186 · Readonly character references

Run `npm run demo -- stage-186-readonly-character-references` with the
.NET 10 SDK, the x86 Windows runtime, Node, and Microsoft Edge. The runner
compares the same compiled C# assembly in each runtime.

The exact platform readonly `MemoryMarshal.GetReference<char>` overload
retains the view's original owner and offset. The verifier tracks this
reference separately from a writable character reference. Unsigned loads
and null checks keep the original position, including an anchored empty
view and a native null address.

The fixture compares strings, live array aliases, empty anchors, and
native-backed views. It also checks that the writable overload retains
its behavior. Writes through the readonly reference, ref returns, and
calls with unverified ref parameters keep located diagnostics. Results
go to `artifacts/stage-186-readonly-character-references/results.json`.
