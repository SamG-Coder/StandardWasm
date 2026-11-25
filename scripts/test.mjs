import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync } from "node:fs";
import { decode, invoke, invokeGenerated } from "./invoke.mjs";
import { demoInputs, listDemos } from "./demos.mjs";
import { chromium } from "playwright-core";
import { compile, root, run } from "./toolchain.mjs";
import { verify } from "./verify.mjs";

process.chdir(root);
mkdirSync("artifacts", { recursive: true });
run("dotnet", ["run", "-c", "Release", "--project", "Verify", "--", "artifacts/oracle.json"]);
const demos = listDemos();
for (const demo of demos) {
    compile(demo.dll, demo.exportType, demo.wasm);
    const { instance } = await WebAssembly.instantiate(readFileSync(demo.wasm));
    assert.equal((demo.generated
        ? await invokeGenerated(instance.exports, demoInputs(demo), demo.generated)
        : invoke(instance.exports, demo.method, demoInputs(demo))), decode(demo.expected), demo.stage);
}
const scalarDemo = demos.find(d => d.assembly === "Sample");
const rejected = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
    "--", scalarDemo.dll, "Sample.CheckedArithmetic", "artifacts/unsupported.wat"], { encoding: "utf8" });
assert.equal(rejected.status, 1);
assert.match(rejected.stderr, /Increment.*IL_[0-9a-f]+.*add\.ovf/);
// 64-bit checked arithmetic must be diagnosed too, with the method and IL offset.
const wideDemo = demos.find(d => d.assembly === "LongIntegers");
const rejectedWide = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
    "--", wideDemo.dll, "LongIntegers.CheckedWide", "artifacts/unsupported-wide.wat"], { encoding: "utf8" });
assert.equal(rejectedWide.status, 1);
assert.match(rejectedWide.stderr, /CheckedWide::Increment\(System\.Int64\) at IL_[0-9a-f]+ \(add\.ovf\)/);
// A checked float-to-int conversion must be diagnosed. Float remainder is lowered exactly since stage 33.
const realsDemo = demos.find(d => d.assembly === "FloatingPoint");
for (const [type, pattern] of [["CheckedConversions", /CheckedConversions::Narrow\(System\.Double\) at IL_[0-9a-f]+ \(conv\.ovf\.i4\)/]]) {
    const rejectedReal = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
        "--", realsDemo.dll, "FloatingPoint." + type, "artifacts/unsupported-real.wat"], { encoding: "utf8" });
    assert.equal(rejectedReal.status, 1);
    assert.match(rejectedReal.stderr, pattern);
}
// A BCL method outside the intrinsic table (decision 0003) must stay an unlinked-call diagnostic.
const mathDemo = demos.find(d => d.assembly === "MathIntrinsics");
const rejectedMath = spawnSync("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Compiler",
    "--", mathDemo.dll, "MathIntrinsics.UnmappedMath", "artifacts/unsupported-math.wat"], { encoding: "utf8" });
assert.equal(rejectedMath.status, 1);
assert.match(rejectedMath.stderr, /UnmappedMath::Power\(System\.Double,System\.Double\) at IL_[0-9a-f]+ \(call\): Unlinked call: System\.Double System\.Math::Pow/);
const cases = JSON.parse(readFileSync("artifacts/oracle.json"));
console.log("Node:", await verify(name => readFileSync("artifacts/" + name + ".wasm"), cases));

const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const browser = await chromium.launch({
    executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
});
try {
    const page = await browser.newPage();
    // Serve only these fixture assets to the isolated test page.
    const assets = new Map([
        ["/invoke.mjs", ["text/javascript", readFileSync("scripts/invoke.mjs")]],
        ["/verify.mjs", ["text/javascript", readFileSync("scripts/verify.mjs")]],
        ...demos.map(d => ["/" + d.assembly, ["application/wasm", readFileSync(d.wasm)]])
    ]);
    await page.route("http://standardwasm.test/**", async route => {
        const path = new URL(route.request().url()).pathname;
        const [contentType, body] = assets.get(path) || ["text/html", "<title>StandardWasm verification</title>"];
        await route.fulfill({ contentType, body });
    });
    await page.goto("http://standardwasm.test/");
    const result = await page.evaluate(async cases => {
        const { verify } = await import("/verify.mjs");
        return verify(async name => (await fetch("/" + name)).arrayBuffer(), cases);
    }, cases);
    console.log("Chromium " + browser.version() + ":", result);
} finally {
    await browser.close();
}
