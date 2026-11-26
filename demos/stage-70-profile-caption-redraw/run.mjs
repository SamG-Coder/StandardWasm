import { run } from "../../scripts/toolchain.mjs";

run(process.execPath, ["scripts/test.mjs"]);
run(process.execPath, ["scripts/winforms-textbox.mjs", "--generated", "--benchmark", "--region-redraw"]);
