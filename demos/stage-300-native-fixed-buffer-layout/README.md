# Stage 300 · Native CLR fixed-buffer layout

A verified sequential native fixed buffer obtains its extent from the declaring field's authentic FixedBufferAttribute, a single primitive nested storage field, and the exact ClassLayout size. Element width and enclosing alignment stay distinct. The declared size must equal the element count times the element width. Custom packing and arbitrary declared sizes retain their diagnostics.

Run `npm run demo -- stage-300-native-fixed-buffer-layout` with a frozen `STANDARDWASM_COMPILER_PATH`. Windows x86 CLR, Node and Edge compare byte, Int32, and UTF-16 buffer extents and enclosing layouts. The UTF-16 case measures the layout used by CoreCLR's version query and must return 276. The unchanged WinForms compile also verified the original CoreLib type's native size and 4-byte alignment. Lowering the fixed-buffer local and its native borrow in the original `GetOSVersion` method remains a separate step.
