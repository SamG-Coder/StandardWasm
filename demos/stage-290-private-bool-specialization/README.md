# Stage 290 · Direct private calls with a known false argument

A direct same-type private call with an independent literal `false` can select a second body that uses the same declared Boolean parameter and calling convention. The compiler proves the callee starts with a guard on that untouched argument and follows normal reachability after folding the guard. The clone shares the same original method metadata and the ordinary body stays available to true, dynamic and address-based calls. Each method has at most one false specialization.

Run `npm run demo -- stage-290-private-bool-specialization` with `STANDARDWASM_COMPILER_PATH` set to a frozen compiler to compare against Windows x86 CLR in Node and Edge. The same fixture covers independent callers, recursive registration, receiver evaluation, mixed use of both bodies, method pointers, an alternate branch entry and typed exception handling, as well as the original unsupported instruction reached by true and dynamic calls.
