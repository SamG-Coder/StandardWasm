# Stage 35 · Managed WinForms control tree

Run from the repository root.

    npm run demo -- stage-35-managed-control-tree
    node scripts/winforms-textbox.mjs --verify

The first command compiles a .NET Standard reference fixture and returns 41. The fixture follows a root-to-child link, a self-link and a child-to-root link. Updating an alias changes the same object. The CLR and generated-code tests also check reference identity, the default-null field value and a null dereference. The CLR reports NullReferenceException, which the generated module surfaces as a Wasm trap.

The portable compiler reserves stable class type indices before resolving fields. It emits a WebAssembly GC recursive type group when classes contain references to other supported sealed classes in the same assembly. Nullable struct fields retain references. ref.eq and ref.is_null cover same-type identity and null tests on typed class values.

The Profile form uses this representation in the browser. The same ProfileApplication.cs creates the Form, TextBox, Button and two Labels on Windows and in Wasm. It inserts the four children through Form.Controls.Add. A linked collection in Wasm retains each child's reference and insertion order, including both Label instances. Edge enumerates it to position the DOM elements and draw the WebGPU preview. The integration test compares all children, bounds, captions, edits and Clear focus.

Run npm run demo -- stage-34-shared-winforms-source for the interactive Profile form. The browser collection implements insertion and traversal for the controls in this stage. Stage 36 adds shared event registration and dispatch.
