# 0080 · Missing dependencies

Status is proposed in #299 (Stage 153). The owner chose to follow .NET Native AOT: detect at
compile time, warn, and generate code that fails at run time as the CLR does. Codex agreed on
ownership and gave design feedback (handoffs 074, codex-299-interface-feedback). The CLR
behaviour was established by probes 1–3 on #299.

## Two different failures

1. **A missing shared framework.** An example is a framework-dependent publish that needs
   `Microsoft.WindowsDesktop.App`. On Windows, hostfxr checks this before `Main`. It
   prints `You must install or update .NET to run this application.` to stderr, exits with
   `0x80008096`, and never runs managed code.
2. **A missing referenced assembly.** An example is `Foo.dll` deleted from the publish
   folder. The CLR runs the app and fails only when the JIT compiles a method that needs
   `Foo`. The call then throws `FileNotFoundException`.

The two are handled at different layers, the host and the compiler.

## Host: framework resolution

`demos/stage-74-executable-entry/frameworks.mjs` resolves `runtimeconfig.json` before
compiling. It reads `runtimeOptions.framework` or `frameworks`, and applies roll-forward
(`Minor` by default; also `LatestPatch`, `Major`, `LatestMinor`, `LatestMajor` and
`Disable`).

- **What the host can supply.** `Microsoft.NETCore.App` 10.0.10 is built in: the compiler
  lowers the platform members it supports by identity and reports any other it reaches.
  Other frameworks need their runtime-pack DLLs, and none are supplied yet.
- **When nothing fits.** The launchers (Stage 74 `runManagedEntry` and Stage 110/151
  `runConsoleEntry`) return state `framework-missing` with exit status `0x80008096`. They
  write hostfxr's message shape to stderr, and the console host writes nothing to stdout.
  The first line and the `Framework: '<name>', version '<v>' (<arch>)` line are exact. The
  app path and location lines name this host, not a disk.
- **Self-contained publishes** list `includedFrameworks`, so they resolve to nothing and
  compile as before.

The follow-up, a resolver that downloads and caches runtime packs, plugs in through the
`available` argument.

## Compiler: deferred stubs (opt in)

`CompileBundleDeferred(bundle, entries, output, location, warnings)` is a new export.
`CompileBundleDetailed` and the other entry points are unchanged and stay strict.

- **Granularity.** In deferred mode, a -4 from `AssemblyBundle.FindOwner` names the method
  being compiled, because that body needs the assembly. The method is recorded, a warning
  `(assembly, MethodDef row, IL offset)` is written, and the program is recompiled. This
  matches the CLR, which fails per method when the JIT compiles it. Probes 1–3 established
  what forces the load:
  - `newobj` of a missing type
  - a call or field access on one
  - a missing base type
  - a value-type field, local or argument of a missing struct
  - a missing type in a callee's signature
  - any of these on a branch that never runs
- **Reference types do not force a load.** A reference-typed local of a missing class
  already compiles: it is untyped storage holding null. In deferred mode, a reference-typed
  field of a missing class is stored as an opaque object reference
  (`BundleImages.DeferMissing`). No instance of that class can exist, so the field only
  ever holds null. Strict mode keeps its -4 for that layout.
- **The stub.** Its body is the synthetic, pre-linked sequence `ldstr message;
  ldstr fileName; newobj System.IO.FileNotFoundException::.ctor(string, string); throw`. The
  constructor is the bundled CoreLib's own (`MissingAssemblyStubs.FindConstructor`). The
  display name `<name>, Version=a.b.c.d, Culture=neutral, PublicKeyToken=<token|null>` is
  formatted from the missing AssemblyRef. The message is `Could not load file or assembly
  '<display name>'. The system cannot find the file specified.` The throw uses the
  managed-object tag (decision 0054), so `catch (FileNotFoundException)` and
  `catch (Exception)` both work. The stub replaces the whole body, so the method's own
  handlers never run, as on the CLR.
- **Type initializers.** A stubbed method does not trigger its type's initializer on entry,
  because the CLR's JIT fails first.

## Still diagnosed

- **No bindable CoreLib.** If CoreLib's `FileNotFoundException(string, string)` is not in
  the bundle, the deferred compile returns the strict located -4.
- **Full public keys.** An AssemblyRef that stores a full public key, rather than a token,
  would need SHA-1 to print its token. It keeps the located -4.
- **Reading the exception.** `e.Message` and `e.FileName` on the CoreLib exception are -2
  on main, independent of this decision: virtual CoreLib exception members are not bound
  yet. The stage therefore compares the caught type and control flow. Message parity is
  checked when those members are bound.
- **Failing type initializers.** A `.cctor` that needs a missing assembly is stubbed. The
  Stage 129 guard then traps, because `TypeInitializationException` cannot be constructed
  yet (decision 0057, #170).
