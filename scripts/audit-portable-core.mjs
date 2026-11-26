import { decode, invoke, managedExceptionType } from "./invoke.mjs";

function manifest(rows, names) {
    const encoder = new TextEncoder();
    const entries = names.map(name => ({ row: rows[name], bytes: encoder.encode(name) }));
    const output = new Uint8Array(entries.reduce((size, entry) => size + 8 + entry.bytes.length, 0));
    const view = new DataView(output.buffer);
    let cursor = 0;
    for (const { row, bytes } of entries) {
        view.setUint32(cursor, row, true);
        view.setUint32(cursor + 4, bytes.length, true);
        output.set(bytes, cursor + 8);
        cursor += 8 + bytes.length;
    }
    return output;
}

const encode = value => typeof value === "bigint" ? { i64: String(value) } :
    Object.is(value, -0) || typeof value === "number" && !Number.isFinite(value) ?
        { f64: Object.is(value, -0) ? "-0" : String(value) } : value;

export async function auditPortable(compilerBytes, fixtures, loadDll) {
    const { instance, module } = await WebAssembly.instantiate(compilerBytes);
    if (WebAssembly.Module.imports(module).length) throw Error("The audit compiler needs runtime imports");
    const compiler = instance.exports;
    for (const name of ["CompileModule", "CreateBytes", "SetByte", "ReadByte"])
        if (typeof compiler[name] !== "function") throw Error("The audit compiler lacks " + name);
    const capacity = 1 << 20;
    const output = compiler.CreateBytes(capacity);
    const location = compiler.CreateDiagnostic && compiler.CompileModuleDetailed && compiler.ReadDiagnostic
        ? compiler.CreateDiagnostic() : undefined;
    const methods = [];
    for (const fixture of fixtures) {
        const dll = { bytes: Array.from(new Uint8Array(await loadDll(fixture.assembly))) };
        for (const { name, cases, transport } of fixture.methods) {
            const report = { assembly: fixture.assembly, method: name, cases: cases.length, passed: 0,
                matchedReturns: 0, matchedManagedExceptions: 0, acceptedLegacyTraps: 0 };
            methods.push(report);
            // Isolate each method so one unsupported export cannot hide the rest.
            const names = transport ? [...new Set([name, "CreateBytes", "SetByte"])] : [name];
            const entries = { bytes: Array.from(manifest(fixture.rows, names)) };
            let length;
            try {
                length = location
                    ? invoke(compiler, "CompileModuleDetailed", [dll, entries, output, location])
                    : invoke(compiler, "CompileModule", [dll, entries, output]);
            } catch (error) {
                report.status = "compiler trap";
                report.error = error.message;
                continue;
            }
            if (length <= 0 || length > capacity) {
                report.status = "compiler diagnostic";
                report.code = length;
                report.location = location ? {
                    assemblyIndex: compiler.ReadDiagnostic(location, 0),
                    methodRow: compiler.ReadDiagnostic(location, 1),
                    ilOffset: compiler.ReadDiagnostic(location, 2)
                } : null;
                continue;
            }
            const bytes = Uint8Array.from({ length }, (_, index) => compiler.ReadByte(output, index));
            let exports;
            try {
                const generated = await WebAssembly.instantiate(bytes);
                if (WebAssembly.Module.imports(generated.module).length) throw Error("Generated output has imports");
                exports = generated.instance.exports;
            } catch (error) {
                report.status = "invalid module";
                report.error = error.message;
                continue;
            }
            for (const test of cases) {
                let actual, failure, managed;
                let threw = false, runtimeTrap = false;
                try { actual = invoke(exports, name, test.inputs); }
                catch (error) {
                    threw = true;
                    runtimeTrap = error instanceof WebAssembly.RuntimeError;
                    managed = managedExceptionType(exports, error);
                    failure = error?.message ?? String(error);
                }
                if (!threw && test.exception == null && Object.is(actual, decode(test.expected))) {
                    report.matchedReturns++;
                } else if (threw && test.expected === null && test.exception != null && managed === test.exception) {
                    report.matchedManagedExceptions++;
                } else if (runtimeTrap && test.expected === null && test.exception != null) {
                    // Preserve the legacy comparison count while exposing the missing CLR exception type.
                    report.acceptedLegacyTraps++;
                } else if (!report.firstMismatch) {
                    report.firstMismatch = {
                        inputs: test.inputs.map(input => input?.bytes ? { byteLength: input.bytes.length } : input),
                        expected: test.expected, actual: encode(actual) ?? null,
                        expectedException: test.exception, actualException: managed ?? undefined,
                        trap: runtimeTrap ? failure : undefined,
                        error: threw && !runtimeTrap && managed === null ? failure : undefined
                    };
                }
            }
            report.passed = report.matchedReturns + report.matchedManagedExceptions + report.acceptedLegacyTraps;
            report.status = report.passed === cases.length ? "pass" : "behavior mismatch";
        }
    }
    const result = { passed: 0, matchedReturns: 0, matchedManagedExceptions: 0, acceptedLegacyTraps: 0, methods };
    for (const method of methods) {
        result.passed += method.passed;
        result.matchedReturns += method.matchedReturns;
        result.matchedManagedExceptions += method.matchedManagedExceptions;
        result.acceptedLegacyTraps += method.acceptedLegacyTraps;
    }
    return result;
}
