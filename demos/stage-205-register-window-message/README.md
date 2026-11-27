# Stage 205 — Registered window messages

An SDK-shaped C# library calls User32 through the same raw ABI as WinForms
Primitives MethodDef 602. The compiler imports the raw leaf while the managed
wrapper still owns clearing and saving last error.

Run on a Windows machine with the x86 .NET runtime and Edge:

```powershell
npm run demo -- stage-205-register-window-message
```

The same fixture assembly runs on Windows x86 .NET, Node and Edge. The comparison
covers 35 cases including duplicate names, ASCII case folding, distinct accented
names, unaligned UTF-16, embedded terminators, the 255-code-unit boundary,
numeric names, and both last-error slots. Windows assigns named IDs for its own
session, so the comparison checks the range and equality pattern of those IDs.
Node and Edge additionally check an unterminated guest pointer and registry
isolation between launches. Evidence is written to
`artifacts/stage-205-register-window-message/results.json`.

Each application launch owns one virtual registration table. The host does not
connect that table to native Windows sessions, other browser launches, or a
cross-application message transport. Native exhaustion parity is unverified.
