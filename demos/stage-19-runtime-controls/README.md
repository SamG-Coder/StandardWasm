# Stage 19 · Runtime controls

From the repository root after installing dependencies:

    npm run demo -- stage-19-runtime-controls
    npm run test:winforms
    npm test

The standalone fixture compiles a class with both int[] and byte[] fields, constructs it and updates both arrays through its methods. Change(511) returns 766. Static literals have no instance storage. Garbage-collected array references retain their types at field loads and stores.

The Stage 14 browser now gets the control count, kind, key and bounds from the running Wasm module. The Windows form consumes the same C# CounterLayout and ControlTree when constructing native controls. The browser build packages shared text resources and compiles the event and layout exports. It does not execute the form or capture geometry during the build.

The click delegate mutates the C# tree. Native WinForms applies the resulting rectangles. Edge reads the same live tree and updates one instanced WebGPU rectangle and the glyph layer. Both hosts report label x coordinates of 32, 56 and 32 after startup, click and Enter. The browser test confirms the position changed in the WebGPU framebuffer and saves the intermediate screenshot to artifacts/winforms/browser-moved.png.

The browser adapter handles the supported Button and Label kinds and property changes for its existing controls. Runtime child insertion/removal, wider WinForms layout features and general managed control types are open.
