# Runnable demos

Run npm ci once from the repository root. Run npm run demo to list available stages. Each stage builds its own .NET Standard DLL and compiles it to a standalone Wasm module. Use npm test for the cross-engine comparison with .NET.

| Stage | Run from repository root | Expected result |
| --- | --- | --- |
| [01 · Scalar methods](stage-01-scalars/README.md) | `npm run demo -- stage-01-scalars` | SumSquares(10) = 385 |
| [02 · Managed arrays](stage-02-managed-arrays/README.md) | `npm run demo -- stage-02-managed-arrays` | PrefixChecksum(2, 3) = 8 |

## Adding a stage

Choose a stable stage number in the tracking issue. Add a stage-NN-name directory containing its project, C# sources, demo.json and README.md. Give each assembly a unique name because it identifies both the artifact and the test module. Copy an existing manifest and specify the assembly, export type, method, inputs and expected value. Document the capability, command and expected result. Add the project reference to Verify/Verify.csproj and CLR comparison cases in Verify/Program.cs, then run the stage command and npm test. Update this index in the same PR. Keep generated files under ignored bin, obj and artifacts directories.
