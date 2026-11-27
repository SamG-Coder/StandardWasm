# Stage 202 · Reference default interface dispatch

The compiler can select a default interface method for a reference receiver after checking concrete and explicit class implementations. It uses the actual method body and the interface's declaring context, for a closed generic MemberRef or a local nongeneric MethodDef. A dynamic dispatch arm validates the nominal class and passes the Wasm reference representation expected by that body.

Run `npm run demo -- stage-202-reference-default-interface`. Supply a prebuilt compiler with `STANDARDWASM_COMPILER_PATH` and `--no-rebuild` to pin verification.

The same .NET Standard 2.1 assembly checks receiver identity, an inherited diamond with a common default, class and explicit priority, null exceptions, a scalar default and a default that forwards through another interface slot. The compiler diagnoses a competing more-specific interface implementation at the constrained call site. Selecting more-specific defaults and applying defaults to value receivers require additional dispatch rules.
