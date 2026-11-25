import { compile } from "./toolchain.mjs";
const args = process.argv.slice(2);
if (args.length !== 3) throw Error("Usage: npm run compile -- assembly.dll Namespace.ExportType output.wasm");
compile(...args);
