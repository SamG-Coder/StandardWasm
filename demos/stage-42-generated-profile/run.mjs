import { run } from "../../scripts/toolchain.mjs";

// --no-rebuild reuses artifacts/StandardWasm.Compiler.wasm from the last npm test (AGENTS.md step 4).
if (!process.argv.includes("--no-rebuild")) run(process.execPath, ["scripts/test.mjs"]);
run(process.execPath, ["scripts/winforms-textbox.mjs", "--verify", "--generated"]);
