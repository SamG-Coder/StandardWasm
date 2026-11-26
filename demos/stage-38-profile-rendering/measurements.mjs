import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { cpus } from "node:os";

export const timingOptions = {
    values: ["Zoë 🚀", "Ada café"], samples: 40, warmup: 12, repeats: 4
};

export async function comparePixels(page, before, after, tolerance, bounds) {
    return page.evaluate(async ({ before, after, tolerance, bounds }) => {
        async function read(bytes) {
            const bitmap = await createImageBitmap(new Blob([Uint8Array.from(bytes)], { type: "image/png" }));
            const context = new OffscreenCanvas(bitmap.width, bitmap.height).getContext("2d");
            context.drawImage(bitmap, 0, 0);
            const area = bounds ?? [0, 0, bitmap.width, bitmap.height];
            return context.getImageData(...area);
        }
        const [a, b] = await Promise.all([read(before), read(after)]);
        if (a.width !== b.width || a.height !== b.height) throw Error("Canvas dimensions disagree");
        let changed = 0, maxDelta = 0;
        for (let i = 0; i < a.data.length; i += 4) {
            let delta = 0;
            for (let c = 0; c < 3; c++) delta = Math.max(delta, Math.abs(a.data[i + c] - b.data[i + c]));
            if (delta > tolerance) changed++;
            maxDelta = Math.max(maxDelta, delta);
        }
        return { pixels: a.width * a.height, changed, maxDelta, tolerance };
    }, { before: Array.from(before), after: Array.from(after), tolerance, bounds });
}

function summarize(values, count) {
    assert.equal(values.length, count);
    assert.ok(values.every(value => Number.isFinite(value) && value >= 0), "Invalid timing sample");
    const sorted = [...values].sort((a, b) => a - b);
    const percentile = fraction => sorted[Math.ceil(sorted.length * fraction) - 1];
    return { median: percentile(.5), p95: percentile(.95), mean: values.reduce((sum, value) => sum + value, 0) / count, count };
}

export async function recordProfileTiming(page, engine, output) {
    const { samples, repeats, warmup, values } = timingOptions;
    const count = samples * repeats;
    const browser = await page.evaluate(options => window.profileTiming.sample(options), timingOptions);
    assert.equal(browser.clock.crossOriginIsolated, true);
    assert.ok(Number.isFinite(browser.clock.observedStepMs) && browser.clock.observedStepMs > 0);
    const native = JSON.parse(readFileSync(output + "/native.json", "utf8"));
    assert.equal(native.sampleCount, count);
    assert.equal(native.warmup, warmup);
    const parity = [];
    const regional = browser.previewRegion !== undefined;
    const redraw = browser.totals.webgpuRedraw !== undefined;
    const snapshotsToCompare = regional ? [...values, ""] : values;
    if (redraw) snapshotsToCompare.push("W".repeat(40));
    for (const [index, value] of snapshotsToCompare.entries()) {
        const snapshots = {};
        const renderers = regional ? ["webgpu", "webgpuRegion", "canvas2d"] : ["webgpu", "canvas2d"];
        if (redraw) renderers.push("webgpuRedraw");
        for (const renderer of renderers) {
            assert.equal(await page.evaluate(({ renderer, value }) => window.profileTiming.render(renderer, value),
                { renderer, value }), "Hello, " + value);
            snapshots[renderer] = await page.locator("#surface").screenshot();
            writeFileSync(output + "/" + renderer + "-" + index + ".png", snapshots[renderer]);
        }
        const comparison = await comparePixels(page, snapshots.webgpu, snapshots.canvas2d, 3);
        assert.ok(comparison.changed / comparison.pixels <= .005, JSON.stringify(comparison));
        const regionComparison = regional ? await comparePixels(page, snapshots.webgpu, snapshots.webgpuRegion, 0) : null;
        if (regionComparison) assert.equal(regionComparison.changed, 0, "Partial and full WebGPU uploads must match.");
        const redrawComparison = redraw ? await comparePixels(page, snapshots.webgpu, snapshots.webgpuRedraw, 0) : null;
        if (redrawComparison) assert.equal(redrawComparison.changed, 0, "Partial and full WebGPU redraws must match.");
        parity.push({ value, ...comparison, ...(regionComparison ? { regional: regionComparison } : {}),
            ...(redrawComparison ? { redraw: redrawComparison } : {}) });
    }
    const metrics = browser.totals;
    const driverQuery = spawnSync("powershell.exe", ["-NoProfile", "-Command",
        "Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion | ConvertTo-Json -Compress"],
        { encoding: "utf8" });
    const drivers = driverQuery.status === 0 && driverQuery.stdout.trim() ? JSON.parse(driverQuery.stdout) : null;
    const webgpuTiming = renderer => ({
        updateMs: summarize(renderer.updateMs, count),
        submitMs: summarize(renderer.submitMs, count),
        queueDrainMs: summarize(renderer.queueDrainMs, repeats),
        ...(redraw ? { paintMs: summarize(renderer.paintMs, count) } : {})
    });
    const fullPixels = browser.width * browser.height;
    const regionalPixels = regional ? browser.previewRegion[2] * browser.previewRegion[3] : 0;
    const evidence = {
        engine, node: process.version, runtime: native.runtime, platform: native.platform, cpu: cpus()[0]?.model.trim(), drivers,
        adapter: browser.adapter, clock: browser.clock, width: browser.width, height: browser.height,
        workload: timingOptions, parity,
        native: { updateMs: summarize(native.updateMs, count), refreshMs: summarize(native.refreshMs, count) },
        webgpu: webgpuTiming(metrics.webgpu),
        ...(regional ? {
            webgpuRegion: webgpuTiming(metrics.webgpuRegion),
            upload: { fullPixels, regionalPixels, reduction: 1 - regionalPixels / fullPixels }
        } : {}),
        ...(redraw ? {
            webgpuRedraw: webgpuTiming(metrics.webgpuRedraw),
            captionsPerEdit: { full: browser.captionCount, partial: 1 }
        } : {}),
        canvas2d: {
            updateMs: summarize(metrics.canvas2d.updateMs, count),
            submitMs: summarize(metrics.canvas2d.submitMs, count),
            ...(redraw ? { paintMs: summarize(metrics.canvas2d.paintMs, count) } : {})
        }
    };
    writeFileSync(output + "/results.json", JSON.stringify(evidence, null, 2) + "\n");
    console.log(JSON.stringify(evidence, null, 2));
}
