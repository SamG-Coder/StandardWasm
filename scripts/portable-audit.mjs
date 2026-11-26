import { createHash } from "node:crypto";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, rmdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { parseArgs } from "node:util";
import { chromium } from "playwright-core";
import { auditPortable } from "./audit-portable-core.mjs";
import { listDemos } from "./demos.mjs";
import { oracleCases } from "./oracle.mjs";
import { root, run } from "./toolchain.mjs";

const { values: options } = parseArgs({ options: {
    compiler: { type: "string", default: "artifacts/StandardWasm.Compiler.wasm" },
    output: { type: "string", default: "artifacts/portable-audit.json" },
    "require-complete": { type: "boolean", default: false }
} });
const compilerBytes = readFileSync(options.compiler);
const oracleBytes = readFileSync(join(root, "artifacts/oracle.json"));
const oracle = oracleCases(JSON.parse(oracleBytes));
const sha256 = bytes => createHash("sha256").update(bytes).digest("hex");
const demos = new Map(listDemos().filter(demo => !demo.runner).map(demo => [demo.assembly, demo]));
const grouped = new Map();
let direct = 0, generated = 0, bootstrap = 0;
for (const test of oracle) {
    if (test.bootstrap) { bootstrap++; continue; }
    if (test.generated) { generated++; continue; }
    if (!demos.has(test.module) || typeof test.method !== "string" || !Object.hasOwn(test, "expected"))
        throw Error("Direct CLR case lacks a registered demo, method or expected value");
    if (!grouped.has(test.module)) grouped.set(test.module, new Map());
    const methods = grouped.get(test.module);
    if (!methods.has(test.method)) methods.set(test.method, { name: test.method, cases: [], transport: false });
    const method = methods.get(test.method);
    method.cases.push({ inputs: test.inputs, expected: test.expected });
    method.transport ||= test.inputs.some(input => Array.isArray(input?.bytes));
    direct++;
}
if (direct === 0) throw Error("The audit found no directly invoked CLR cases");

const fixtures = [...grouped].map(([assembly, methods]) => ({ assembly, methods: [...methods.values()] }));
const inputs = fixtures.map(({ assembly, methods }) => {
    const demo = demos.get(assembly);
    const names = new Set(methods.map(method => method.name));
    if (methods.some(method => method.transport)) {
        names.add("CreateBytes");
        names.add("SetByte");
    }
    return { Assembly: assembly, Dll: demo.dll, ExportType: demo.exportType, Names: [...names] };
});

const scratch = mkdtempSync(join(root, "artifacts", "portable-audit-"));
const request = join(scratch, "request.json");
const response = join(scratch, "rows.json");
try {
    writeFileSync(request, JSON.stringify(inputs));
    run("dotnet", ["run", "-c", "Release", "--no-build", "--project", "Verify", "--",
        "--portable-audit", request, response]);
    const rows = JSON.parse(readFileSync(response));
    for (const fixture of fixtures) fixture.rows = rows[fixture.assembly];
} finally {
    rmSync(request, { force: true });
    rmSync(response, { force: true });
    rmdirSync(scratch);
}

const dlls = new Map(inputs.map(input => [input.Assembly, readFileSync(input.Dll)]));
const nodeResult = await auditPortable(compilerBytes, fixtures, assembly => dlls.get(assembly));
const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const browser = await chromium.launch({
    executablePath: process.env.BROWSER_PATH || (existsSync(edge) ? edge : undefined)
});
let browserResult, browserVersion;
try {
    const page = await browser.newPage();
    const assets = new Map([
        ["/audit-portable-core.mjs", ["text/javascript", readFileSync(join(root, "scripts/audit-portable-core.mjs"))]],
        ["/invoke.mjs", ["text/javascript", readFileSync(join(root, "scripts/invoke.mjs"))]],
        ["/compiler", ["application/wasm", compilerBytes]],
        ...[...dlls].map(([assembly, image]) => ["/dll/" + assembly, ["application/octet-stream", image]])
    ]);
    await page.route("http://portable-audit.test/**", async route => {
        const path = new URL(route.request().url()).pathname;
        const asset = assets.get(path);
        if (asset) await route.fulfill({ contentType: asset[0], body: asset[1] });
        else if (path === "/") await route.fulfill({ contentType: "text/html", body: "<title>Portable compiler audit</title>" });
        else await route.fulfill({ status: 404 });
    });
    await page.goto("http://portable-audit.test/");
    browserVersion = browser.version();
    browserResult = await page.evaluate(async fixtures => {
        const { auditPortable } = await import("/audit-portable-core.mjs");
        async function read(path) {
            const response = await fetch(path);
            if (!response.ok) throw Error("Could not load audit asset " + path);
            return response.arrayBuffer();
        }
        return auditPortable(await read("/compiler"), fixtures, assembly => read("/dll/" + assembly));
    }, fixtures);
} finally {
    await browser.close();
}

const evidence = {
    compiler: { path: resolve(options.compiler), bytes: compilerBytes.length, sha256: sha256(compilerBytes) },
    corpus: {
        direct, generated, bootstrap, oracleSha256: sha256(oracleBytes),
        dlls: Object.fromEntries([...dlls].map(([assembly, image]) => [assembly, sha256(image)]))
    },
    node: { version: process.version, ...nodeResult },
    browser: { version: browserVersion, ...browserResult }
};
mkdirSync(dirname(resolve(options.output)), { recursive: true });
writeFileSync(options.output, JSON.stringify(evidence, null, 2) + "\n");
console.log(`Direct CLR comparisons passed: Node ${nodeResult.passed}/${direct}, browser ${browserResult.passed}/${direct}`);
console.log(`Existing npm test fixtures outside this audit: ${generated} generated-code cases, ${bootstrap} bootstrap case`);
console.log("Wrote " + options.output);
if (options["require-complete"] && (nodeResult.passed !== direct || browserResult.passed !== direct))
    process.exitCode = 1;
