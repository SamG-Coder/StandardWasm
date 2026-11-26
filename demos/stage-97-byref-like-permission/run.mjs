import assert from "node:assert/strict";
import { mkdirSync, readFileSync } from "node:fs";
import { compile, root, run } from "../../scripts/toolchain.mjs";
import { invokeGenerated } from "../../scripts/invoke.mjs";

process.chdir(root);
const fixture = "demos/stage-97-byref-like-permission";
const stage = "demos/stage-13-complete-bootstrap";
run("dotnet", ["build", "-c", "Release", fixture + "/ByRefPermissionFixture.csproj"]);
run("dotnet", ["build", "-c", "Release", stage + "/BootstrapLibrary.csproj"]);
mkdirSync("artifacts", { recursive: true });
const compiler = "artifacts/ByRefPermissionBootstrap.wasm";
compile(stage + "/bin/Release/netstandard2.0/BootstrapLibrary.dll", "BootstrapLibrary.CompilerExports", compiler);
const bin = fixture + "/bin/Release/net10.0/";
const images = ["ByRefPermissionFixture.dll", "ByRefPermissionDependency.dll"];
const bundle = Buffer.concat(images.flatMap(name => {
    const image = readFileSync(bin + name);
    const header = Buffer.alloc(4);
    header.writeUInt32LE(image.length);
    return [header, image];
}));
const inputs = [{ bytes: [...bundle] }, { name: [82, 117, 110] }];
const { instance: { exports } } = await WebAssembly.instantiate(readFileSync(compiler));
const result = await invokeGenerated(exports, inputs, { bundle: true, capacity: 32768, arguments: [0] });
assert.equal(result, 23);
console.log("The .NET 10 permission fixture returned 23.");
