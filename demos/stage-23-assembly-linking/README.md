# Stage 23 · Linking managed assemblies

Run from the repository root after installing dependencies.

    npm run demo -- stage-23-assembly-linking
    npm test

The build produces BundleLibrary.dll, SharedMath.dll and LeafMath.dll through ordinary project references. The compiler running in Wasm links Calculate, chooses the exact Scale overloads, and follows Blend to its dependency in LeafMath. The generated module returns 89 for Calculate(9) and has zero runtime imports.

CompileBundle accepts a sequence of little-endian u32 lengths followed by complete DLL images. The first image owns the export manifest. The remaining images supply dependencies. The compiler resolves TypeRef, AssemblyRef and MemberRef metadata by exact assembly name and version, public top-level type and exact method signature. It assigns function indices in reachable call order while retaining assembly identity alongside each MethodDef row.

The portable bundle path initially supports unsigned, culture-neutral dependencies with scalar static methods. It rejects dependencies with initializers. Missing or mismatched identities return -4, ambiguous matches return -5, missing matching methods return -6, and malformed bundles return -1. The test verifies destination preservation for those failures. Dependency record order does not change output bytes for unambiguous references. Cross-assembly managed types, constructors, instance calls, stronger assembly identity support and framework linking remain open.

The self-recompiled compiler exposes CompileBundle and reruns the linked library cases in Node and Edge.
