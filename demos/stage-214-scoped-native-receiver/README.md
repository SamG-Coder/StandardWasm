# Stage 214 — Scoped native instance receivers

A sequential ref struct whose storage contains no managed references can
call a verified instance method against its original native frame slot.
The compiler validates every use of the receiver in the original body.
The method can read its own native-word field or borrow its address for
a local, immediately verified indirect operation. Once a borrow selects
the frame storage, compatible field readers use the same slot.

The ordinary instance method body still performs comparison, calls and
control flow. A call from a finally handler receives the same slot that
the caller used earlier in the protected region.

After building the compiler, run on Windows with x86 .NET and Edge.

```powershell
npm run demo -- stage-214-scoped-native-receiver
```

The fixture checks normal and exceptional finally handling, independent
owners, a previously borrowed pointer, and the release side effect. A
method that returns the interior pointer is diagnosed when called on a
native-backed frame owner. Results are written to
`artifacts/stage-214-scoped-native-receiver/results.json`.
