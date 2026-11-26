// Hand-written lowerings of the stage 77 scenarios under three candidate memory models (#167).
// The scenario bodies are shared text. Each model supplies only the array, pin and slice primitives, so any
// difference against the CLR comes from the memory model and not from the scenario code.
//
// All models share one linear-memory shadow stack for address-taken locals and stackalloc. No Wasm model can
// take the address of a Wasm local, so that part is common ground, not a candidate difference.

const models = {
    // A: payloads are Wasm GC arrays. `fixed` copies the array into linear memory and copies it back on exit,
    // because GC arrays have no address.
    A: {
        types: `
  (type $B (array (mut i8)))
  (type $I (array (mut i32)))
  (type $C (array (mut i16)))`,
        B: "(ref $B)", I: "(ref $I)", C: "(ref $C)",
        helpers: `
  (func $newB (param $n i32) (result (ref $B)) (array.new_default $B (local.get $n)))
  (func $newI (param $n i32) (result (ref $I)) (array.new_default $I (local.get $n)))
  (func $newC (param $n i32) (result (ref $C)) (array.new_default $C (local.get $n)))
  (func $lenB (param $a (ref $B)) (result i32) (array.len (local.get $a)))
  (func $lenC (param $a (ref $C)) (result i32) (array.len (local.get $a)))
  (func $getB (param $a (ref $B)) (param $i i32) (result i32) (array.get_u $B (local.get $a) (local.get $i)))
  (func $setB (param $a (ref $B)) (param $i i32) (param $v i32) (array.set $B (local.get $a) (local.get $i) (local.get $v)))
  (func $getI (param $a (ref $I)) (param $i i32) (result i32) (array.get $I (local.get $a) (local.get $i)))
  (func $setI (param $a (ref $I)) (param $i i32) (param $v i32) (array.set $I (local.get $a) (local.get $i) (local.get $v)))
  (func $setC (param $a (ref $C)) (param $i i32) (param $v i32) (array.set $C (local.get $a) (local.get $i) (local.get $v)))
  ;; pin: copy in, return the address. unpin: copy back.
  (func $pinB (param $a (ref $B)) (result i32) (local $p i32) (local $i i32)
    (local.set $p (call $alloc (array.len (local.get $a))))
    (block $done (loop $copy
      (br_if $done (i32.ge_u (local.get $i) (array.len (local.get $a))))
      (i32.store8 (i32.add (local.get $p) (local.get $i)) (array.get_u $B (local.get $a) (local.get $i)))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $copy)))
    (local.get $p))
  (func $unpinB (param $a (ref $B)) (param $p i32) (local $i i32)
    (block $done (loop $copy
      (br_if $done (i32.ge_u (local.get $i) (array.len (local.get $a))))
      (array.set $B (local.get $a) (local.get $i) (i32.load8_u (i32.add (local.get $p) (local.get $i))))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $copy))))
  (func $pinI (param $a (ref $I)) (result i32) (local $p i32) (local $i i32)
    (local.set $p (call $alloc (i32.shl (array.len (local.get $a)) (i32.const 2))))
    (block $done (loop $copy
      (br_if $done (i32.ge_u (local.get $i) (array.len (local.get $a))))
      (i32.store (i32.add (local.get $p) (i32.shl (local.get $i) (i32.const 2))) (array.get $I (local.get $a) (local.get $i)))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $copy)))
    (local.get $p))
  (func $unpinI (param $a (ref $I)) (param $p i32) (local $i i32)
    (block $done (loop $copy
      (br_if $done (i32.ge_u (local.get $i) (array.len (local.get $a))))
      (array.set $I (local.get $a) (local.get $i) (i32.load (i32.add (local.get $p) (i32.shl (local.get $i) (i32.const 2)))))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $copy))))
  (func $pinC (param $a (ref $C)) (result i32) (local $p i32) (local $i i32)
    (local.set $p (call $alloc (i32.shl (array.len (local.get $a)) (i32.const 1))))
    (block $done (loop $copy
      (br_if $done (i32.ge_u (local.get $i) (array.len (local.get $a))))
      (i32.store16 (i32.add (local.get $p) (i32.shl (local.get $i) (i32.const 1))) (array.get_u $C (local.get $a) (local.get $i)))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $copy)))
    (local.get $p))
  (func $unpinC (param $a (ref $C)) (param $p i32))
  ;; A Span over a GC array is (array, offset). It cannot share the pointer representation used for stackalloc.
  (func $sliceSetB (param $a (ref $B)) (param $offset i32) (param $i i32) (param $v i32)
    (array.set $B (local.get $a) (i32.add (local.get $offset) (local.get $i)) (local.get $v)))`
    },

    // B: every object lives in linear memory as [length:i32][payload]. A reference is an address, `fixed` is the
    // payload address, and nothing moves. Reclamation needs a custom collector with stack scanning, which this
    // prototype does not implement (bump allocation only).
    B: {
        types: "",
        B: "i32", I: "i32", C: "i32",
        helpers: linearHelpers(
            "(i32.load (local.get $a))",
            "(i32.add (local.get $a) (i32.const 4))",
            size => `(local.set $a (call $alloc (i32.add (i32.const 4) ${size})))
    (i32.store (local.get $a) (local.get $n)) (local.get $a)`,
            "i32")
    },

    // C: reference-holding objects stay in Wasm GC. A blittable array is a GC wrapper {payload address, length}
    // whose payload is in linear memory. A reference is a GC ref, `fixed` is the payload address, and nothing moves.
    // The payload is reclaimed when the wrapper is collected (measured separately with FinalizationRegistry).
    C: {
        types: `
  (type $Obj (struct (field $ptr i32) (field $len i32)))`,
        B: "(ref $Obj)", I: "(ref $Obj)", C: "(ref $Obj)",
        helpers: linearHelpers(
            "(struct.get $Obj $len (local.get $a))",
            "(struct.get $Obj $ptr (local.get $a))",
            size => `(struct.new $Obj (call $alloc ${size}) (local.get $n))`,
            "(ref $Obj)")
    }
};

// B and C share every primitive except how the length and payload address are found, and how allocation works.
function linearHelpers(length, payload, allocate, ref) {
    const access = (name, shift, load, store) => `
  (func $get${name} (param $a ${ref}) (param $i i32) (result i32)
    (if (i32.ge_u (local.get $i) ${length}) (then (unreachable)))
    (${load} (i32.add ${payload} (i32.shl (local.get $i) (i32.const ${shift})))))
  (func $set${name} (param $a ${ref}) (param $i i32) (param $v i32)
    (if (i32.ge_u (local.get $i) ${length}) (then (unreachable)))
    (${store} (i32.add ${payload} (i32.shl (local.get $i) (i32.const ${shift}))) (local.get $v)))
  (func $pin${name} (param $a ${ref}) (result i32) ${payload})
  (func $unpin${name} (param $a ${ref}) (param $p i32))`;
    const create = (name, shift) => `
  (func $new${name} (param $n i32) (result ${ref}) ${ref === "i32" ? "(local $a i32)" : ""}
    ${allocate(`(i32.shl (local.get $n) (i32.const ${shift}))`)})`;
    return create("B", 0) + create("I", 2) + create("C", 1) +
        access("B", 0, "i32.load8_u", "i32.store8") + access("I", 2, "i32.load", "i32.store") +
        access("C", 1, "i32.load16_u", "i32.store16") + `
  (func $lenB (param $a ${ref}) (result i32) ${length})
  (func $lenC (param $a ${ref}) (result i32) ${length})
  ;; A Span over the array is a plain (address, length), the same representation as a stackalloc Span.
  (func $sliceSetB (param $a ${ref}) (param $offset i32) (param $i i32) (param $v i32)
    (i32.store8 (i32.add (i32.add ${payload} (local.get $offset)) (local.get $i)) (local.get $v)))`;
}

// Scenario bodies follow demos/stage-77-pointer-memory-model/Patterns.cs line by line.
const scenarios = m => `
  (func $fillB (param $n i32) (result ${m.B}) (local $a ${m.B}) (local $i i32)
    (local.set $a (call $newB (local.get $n)))
    (block $done (loop $fill
      (br_if $done (i32.ge_s (local.get $i) (local.get $n)))
      (call $setB (local.get $a) (local.get $i) (i32.and (i32.add (i32.mul (local.get $i) (i32.const 7)) (i32.const 3)) (i32.const 255)))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $fill)))
    (local.get $a))

  (func (export "FixedSum") (param $n i32) (result i32) (local $a ${m.B}) (local $p i32) (local $i i32) (local $sum i32)
    (local.set $a (call $fillB (local.get $n)))
    (local.set $p (call $pinB (local.get $a)))
    (block $done (loop $sum
      (br_if $done (i32.ge_s (local.get $i) (local.get $n)))
      (local.set $sum (i32.add (local.get $sum) (i32.load8_u (i32.add (local.get $p) (local.get $i)))))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $sum)))
    (call $unpinB (local.get $a) (local.get $p))
    (local.get $sum))

  (func (export "FixedAliasing") (param $seed i32) (result i32) (local $a ${m.B}) (local $p i32) (local $seen i32)
    (local.set $a (call $newB (i32.const 4)))
    (local.set $p (call $pinB (local.get $a)))
    (call $setB (local.get $a) (i32.const 0) (i32.and (local.get $seed) (i32.const 255)))
    (local.set $seen (i32.load8_u (local.get $p)))
    (i32.store8 (i32.add (local.get $p) (i32.const 1)) (i32.add (local.get $seed) (i32.const 1)))
    (local.set $seen (i32.add (i32.mul (local.get $seen) (i32.const 1000)) (call $getB (local.get $a) (i32.const 1))))
    (call $unpinB (local.get $a) (local.get $p))
    (i32.sub (i32.add (i32.mul (local.get $seen) (i32.const 10)) (call $getB (local.get $a) (i32.const 1)))
             (call $getB (local.get $a) (i32.const 0))))

  (func $writeTriple (param $t i32) (param $v i32)
    (i32.store (i32.sub (local.get $t) (i32.const 4)) (local.get $v))
    (i32.store (local.get $t) (i32.add (local.get $v) (i32.const 1)))
    (i32.store (i32.add (local.get $t) (i32.const 4)) (i32.add (local.get $v) (i32.const 2))))
  (func (export "PointerIntoCallee") (param $v i32) (result i32) (local $a ${m.I}) (local $p i32)
    (local.set $a (call $newI (i32.const 3)))
    (local.set $p (call $pinI (local.get $a)))
    (call $writeTriple (i32.add (local.get $p) (i32.const 4)) (local.get $v))
    (call $unpinI (local.get $a) (local.get $p))
    (i32.add (i32.add (i32.mul (call $getI (local.get $a) (i32.const 0)) (i32.const 100))
                      (i32.mul (call $getI (local.get $a) (i32.const 1)) (i32.const 10)))
             (call $getI (local.get $a) (i32.const 2))))

  (func (export "FixedString") (param $r i32) (result i32) (local $s ${m.C}) (local $p i32) (local $i i32) (local $sum i32)
    (local.set $s (call $newC (i32.add (local.get $r) (i32.const 7))))
    (block $done (loop $w (br_if $done (i32.ge_s (local.get $i) (local.get $r)))
      (call $setC (local.get $s) (local.get $i) (i32.const 87)) (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $w)))
    ${["i", "n", "F", "o", "r", "m", "s"].map((ch, k) =>
        `(call $setC (local.get $s) (i32.add (local.get $r) (i32.const ${k})) (i32.const ${ch.charCodeAt(0)}))`).join("\n    ")}
    (local.set $p (call $pinC (local.get $s)))
    (local.set $i (i32.const 0))
    (block $done (loop $sum (br_if $done (i32.ge_s (local.get $i) (call $lenC (local.get $s))))
      (local.set $sum (i32.add (local.get $sum) (i32.load16_u (i32.add (local.get $p) (i32.shl (local.get $i) (i32.const 1))))))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $sum)))
    (call $unpinC (local.get $s) (local.get $p))
    (local.get $sum))

  (func (export "SpanSlice") (param $seed i32) (result i32) (local $a ${m.B})
    (local.set $a (call $newB (i32.const 8)))
    (call $sliceSetB (local.get $a) (i32.const 2) (i32.const 0) (local.get $seed))
    (call $sliceSetB (local.get $a) (i32.const 2) (i32.const 2) (i32.mul (local.get $seed) (i32.const 2)))
    (i32.add (i32.add (i32.mul (call $getB (local.get $a) (i32.const 2)) (i32.const 1000))
                      (call $getB (local.get $a) (i32.const 4))) (i32.const 3)))

  (func (export "StackBuffer") (param $c i32) (result i32) (local $buf i32) (local $i i32) (local $sum i32)
    (global.set $sp (i32.sub (global.get $sp) (i32.const 64)))
    (local.set $buf (global.get $sp))
    (memory.fill (local.get $buf) (i32.const 0) (i32.const 64))
    (block $done (loop $w (br_if $done (i32.ge_s (local.get $i) (i32.const 16)))
      (i32.store (i32.add (local.get $buf) (i32.shl (local.get $i) (i32.const 2))) (i32.mul (local.get $i) (local.get $c)))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $w)))
    (local.set $i (i32.const 0))
    (block $done (loop $r (br_if $done (i32.ge_s (local.get $i) (i32.const 16)))
      (local.set $sum (i32.add (local.get $sum) (i32.load (i32.add (local.get $buf) (i32.shl (local.get $i) (i32.const 2))))))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $r)))
    (global.set $sp (i32.add (global.get $sp) (i32.const 64)))
    (local.get $sum))

  (func $mutate (param $p i32) (i32.store offset=4 (local.get $p) (i32.add (i32.load offset=4 (local.get $p)) (i32.const 1))))
  (func (export "LocalStructAddress") (param $x i32) (result i32) (local $pt i32) (local $r i32)
    (global.set $sp (i32.sub (global.get $sp) (i32.const 8)))
    (local.set $pt (global.get $sp))
    (i64.store (local.get $pt) (i64.const 0))
    (i32.store (local.get $pt) (local.get $x))
    (i32.store offset=4 (local.get $pt) (i32.mul (local.get $x) (i32.const 2)))
    (call $mutate (local.get $pt))
    (local.set $r (i32.add (i32.mul (i32.load (local.get $pt)) (i32.const 100)) (i32.load offset=4 (local.get $pt))))
    (global.set $sp (i32.add (global.get $sp) (i32.const 8)))
    (local.get $r))

  (func (export "StructArrayElement") (param $x i32) (result i32) (local $a ${m.I}) (local $p i32)
    (local.set $a (call $newI (i32.const 4)))
    (local.set $p (call $pinI (local.get $a)))
    (i32.store offset=8 (local.get $p) (local.get $x))
    (i32.store offset=12 (local.get $p) (i32.add (local.get $x) (i32.const 5)))
    (call $unpinI (local.get $a) (local.get $p))
    (i32.add (i32.mul (call $getI (local.get $a) (i32.const 2)) (i32.const 100)) (call $getI (local.get $a) (i32.const 3))))`;

// Cost probes. The JS host times each export.
const benchmarks = m => `
  ;; Read every element through the managed path (ldelem), \`rounds\` times.
  (func (export "benchAccess") (param $n i32) (param $rounds i32) (result i32) (local $a ${m.B}) (local $i i32) (local $r i32) (local $sum i32)
    (call $reset)
    (local.set $a (call $fillB (local.get $n)))
    (block $out (loop $round (br_if $out (i32.ge_s (local.get $r) (local.get $rounds)))
      (local.set $i (i32.const 0))
      (block $done (loop $read (br_if $done (i32.ge_s (local.get $i) (local.get $n)))
        (local.set $sum (i32.add (local.get $sum) (call $getB (local.get $a) (local.get $i))))
        (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $read)))
      (local.set $r (i32.add (local.get $r) (i32.const 1))) (br $round)))
    (local.get $sum))
  ;; Enter and leave \`fixed\` over one array \`rounds\` times, touching one element each time.
  (func (export "benchFixed") (param $n i32) (param $rounds i32) (result i32) (local $a ${m.B}) (local $p i32) (local $r i32) (local $sum i32) (local $mark i32)
    (call $reset)
    (local.set $a (call $fillB (local.get $n)))
    (local.set $mark (global.get $heap))
    (block $out (loop $round (br_if $out (i32.ge_s (local.get $r) (local.get $rounds)))
      (local.set $p (call $pinB (local.get $a)))
      (local.set $sum (i32.add (local.get $sum) (i32.load8_u (local.get $p))))
      (call $unpinB (local.get $a) (local.get $p))
      (global.set $heap (local.get $mark))
      (local.set $r (i32.add (local.get $r) (i32.const 1))) (br $round)))
    (local.get $sum))
  ;; Allocate \`count\` small arrays (new byte[size]) and keep only a checksum.
  (func (export "benchAlloc") (param $count i32) (param $size i32) (result i32) (local $a ${m.B}) (local $k i32) (local $sum i32)
    (call $reset)
    (block $out (loop $alloc (br_if $out (i32.ge_s (local.get $k) (local.get $count)))
      (local.set $a (call $newB (local.get $size)))
      (call $setB (local.get $a) (i32.const 0) (local.get $k))
      (local.set $sum (i32.add (local.get $sum) (call $getB (local.get $a) (i32.const 0))))
      (local.set $k (i32.add (local.get $k) (i32.const 1))) (br $alloc)))
    (local.get $sum))`;

export function buildWat(name) {
    const m = models[name];
    return `(module
  (memory (export "memory") 512)
  ;; The shadow stack grows down from 64 KiB. The bump heap starts above it.
  (global $sp (mut i32) (i32.const 65536))
  (global $heap (mut i32) (i32.const 65536))
  ${m.types}
  (func $alloc (param $size i32) (result i32) (local $p i32)
    (local.set $p (global.get $heap))
    (global.set $heap (i32.and (i32.add (i32.add (local.get $p) (local.get $size)) (i32.const 7)) (i32.const -8)))
    (if (i32.gt_u (global.get $heap) (i32.mul (memory.size) (i32.const 65536)))
      (then (if (i32.eq (memory.grow (i32.add (i32.shr_u (i32.sub (global.get $heap) (i32.mul (memory.size) (i32.const 65536))) (i32.const 16)) (i32.const 1))) (i32.const -1)) (then (unreachable)))))
    ;; CLR allocations are zero-initialized. The bump heap reuses memory after reset, so clear it explicitly.
    (memory.fill (local.get $p) (i32.const 0) (local.get $size))
    (local.get $p))
  (func $reset (export "reset") (global.set $heap (i32.const 65536)))
  ${m.helpers}
  ${scenarios(m)}
  ${benchmarks(m)}
  ${name === "C" ? `;; Hands one wrapper to JS so the host can test reclamation through FinalizationRegistry.
  (func (export "newTrackedBytes") (param $n i32) (result (ref $Obj)) (call $newB (local.get $n)))` : ""}
)`;
}

export const modelNames = Object.keys(models);
