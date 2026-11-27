// Regenerates demos/stage-74-executable-entry/win32-upcase.mjs from the Windows
// RtlUpcaseUnicodeChar table that this stage's oracle records. Run the stage
// verifier first, then: node demos/stage-360-launch-long-paths/generate-upcase.mjs
import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { root } from "../../scripts/toolchain.mjs";

const { upcase } = JSON.parse(readFileSync(join(root, "artifacts/stage-360-launch-long-paths/oracle.json"), "utf8"));
if (!Array.isArray(upcase) || upcase.length !== 65536) throw Error("The oracle has no complete upcase table.");

// Runs of [first unit, count, delta, stride] with stride 1 (consecutive units) or
// 2 (alternating upper/lower pairs, where the units between map to themselves).
const runs = [];
for (let unit = 0; unit < 65536;) {
    const delta = upcase[unit] - unit;
    if (!delta) { unit++; continue; }
    let consecutive = 1;
    while (unit + consecutive < 65536 && upcase[unit + consecutive] - (unit + consecutive) === delta) consecutive++;
    let alternating = 1;
    while (unit + 2 * alternating < 65536 && upcase[unit + 2 * alternating] - (unit + 2 * alternating) === delta &&
        upcase[unit + 2 * alternating - 1] === unit + 2 * alternating - 1) alternating++;
    if (alternating > consecutive) { runs.push([unit, alternating, delta, 2]); unit += 2 * alternating - 1; }
    else { runs.push([unit, consecutive, delta, 1]); unit += consecutive; }
}
const lines = [];
for (let index = 0; index < runs.length; index += 6)
    lines.push("    " + runs.slice(index, index + 6).map(run => "[" + run.join(", ") + "]").join(", ") + ",");

writeFileSync(join(root, "demos/stage-74-executable-entry/win32-upcase.mjs"), `// Generated from Windows RtlUpcaseUnicodeChar for every UTF-16 code unit, by
// demos/stage-360-launch-long-paths/generate-upcase.mjs; the Stage 360 verifier compares it.
// Each run is [first unit, count, delta, stride]: units first + k * stride map to
// themselves plus delta. Units outside every run map to themselves. NTFS compares
// names through the same simple, single-unit table (its $UpCase file), so
// surrogates, ß and titlecase digraphs are never expanded or merged.
const upcaseRuns = [
${lines.join("\n")}
];

const upcase = new Uint16Array(65536);
for (let unit = 0; unit < upcase.length; unit++) upcase[unit] = unit;
for (const [first, count, delta, stride] of upcaseRuns)
    for (let index = 0; index < count; index++) upcase[first + index * stride] += delta;

export const win32UpcaseRunCount = upcaseRuns.length;

export function win32Upcase(unit) {
    return upcase[unit];
}

// Upcases each UTF-16 unit independently, as Win32 case-insensitive name comparison does.
export function foldWin32Name(name) {
    let folded = "";
    for (let index = 0; index < name.length; index++) folded += String.fromCharCode(upcase[name.charCodeAt(index)]);
    return folded;
}
`);
console.log("Wrote " + runs.length + " upcase runs.");
