# Stage 15 · Managed instances

From the repository root after installing dependencies:

    npm run demo -- stage-15-managed-instances
    npm run test:winforms
    npm test

The compiler running inside Wasm reads ObjectLibrary.dll and emits Isolated. It constructs two instances and calls methods on them independently. The expected result is Isolated(10) = 19. The compiler resolves TypeDef, Field and MethodDef ownership and emits a Wasm GC struct for each supported class. It checks reference types at calls, field accesses and stack merges. Nonvirtual callvirt includes a null check.

The supported shape is a sealed class with System.Object as its direct base, i32 instance fields and nonvirtual methods with supported signatures. The compiler recognizes the base System.Object constructor when the type reference identifies a supported platform assembly. Inheritance, virtual overrides, interfaces, general objects, reference-valued fields and full assembly identity resolution are open. The current internal type representation allows up to 252 distinct class layouts per generated module. Browser-side references should come from the corresponding module's typed factory.

Stage 14 now uses CounterState for the same handler on Windows and in Wasm. The browser keeps the opaque Wasm reference and calls exported methods to advance and read it. The UI test checks that two generated instances remain independent and a null instance call traps. The .NET Standard fixture tests constructor initialization, fields, repeated calls, overflow and rejection of malformed field and method receivers. An extern-alias fixture verifies that a same-named third-party System.Object base runs its own constructor on the CLR and produces an unsupported-IL diagnostic before the portable compiler emits a module. The self-recompiled compiler reruns the same generated-code cases.
