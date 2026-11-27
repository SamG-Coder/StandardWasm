# Stage 300 · Native CLR fixed-buffer layout

A verified sequential native fixed buffer obtains its extent from the declaring field's authentic FixedBufferAttribute, a single primitive nested storage field, and the exact ClassLayout size. Element width and enclosing alignment stay distinct. The declared size must equal the element count times the element width. Custom packing and arbitrary declared sizes retain their diagnostics.

Run `npm run demo -- stage-300-native-fixed-buffer-layout` with a frozen `STANDARDWASM_COMPILER_PATH`. Windows x86 CLR, Node and Edge compare ordinary byte and Int32 buffer extents and enclosing layouts. This verifies the native carrier used by the subsequent Mono assembly-name boundary.
