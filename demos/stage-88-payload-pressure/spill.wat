;; Stage 88, frame cost of model B's roots (decision 0025 follow-up). A recursive sum over a complete binary tree,
;; written three ways with the same shape:
;;   gcSum:      nodes are Wasm GC structs, references live in Wasm locals (model C, the engine traces frames);
;;   linearSum:  nodes in linear memory, references are i32 addresses, no rooting (a lower bound, unsafe under a GC);
;;   spillSum:   as linearSum, but every reference live across a call is stored in a shadow-stack slot first and
;;               the slot is cleared on return, which is what B's precise collector needs to see roots.
(module
  (type $Node (struct (field $left (ref null $Node)) (field $right (ref null $Node)) (field $value i32)))
  (memory (export "memory") 256 16384)
  (global $heap (mut i32) (i32.const 65536))
  (global $sp (mut i32) (i32.const 65536))         ;; shadow stack grows down from 64 KiB
  (global $gcRoot (mut (ref null $Node)) (ref.null $Node))
  (global $linearRoot (mut i32) (i32.const 0))

  (func $buildGc (param $depth i32) (param $v i32) (result (ref null $Node))
    (if (result (ref null $Node)) (i32.eqz (local.get $depth))
      (then (ref.null $Node))
      (else (struct.new $Node
        (call $buildGc (i32.sub (local.get $depth) (i32.const 1)) (i32.mul (local.get $v) (i32.const 2)))
        (call $buildGc (i32.sub (local.get $depth) (i32.const 1)) (i32.add (i32.mul (local.get $v) (i32.const 2)) (i32.const 1)))
        (local.get $v)))))
  ;; Linear node: [left:i32][right:i32][value:i32]
  (func $buildLinear (param $depth i32) (param $v i32) (result i32) (local $p i32)
    (if (i32.eqz (local.get $depth)) (then (return (i32.const 0))))
    (local.set $p (global.get $heap))
    (global.set $heap (i32.add (local.get $p) (i32.const 12)))
    (if (i32.gt_u (global.get $heap) (i32.mul (memory.size) (i32.const 65536)))
      (then (drop (memory.grow (i32.const 16)))))
    (i32.store offset=8 (local.get $p) (local.get $v))
    (i32.store (local.get $p) (call $buildLinear (i32.sub (local.get $depth) (i32.const 1)) (i32.mul (local.get $v) (i32.const 2))))
    (i32.store offset=4 (local.get $p) (call $buildLinear (i32.sub (local.get $depth) (i32.const 1))
      (i32.add (i32.mul (local.get $v) (i32.const 2)) (i32.const 1))))
    (local.get $p))
  (func (export "build") (param $depth i32)
    (global.set $gcRoot (call $buildGc (local.get $depth) (i32.const 1)))
    (global.set $linearRoot (call $buildLinear (local.get $depth) (i32.const 1))))

  (func $gcSum (param $n (ref null $Node)) (result i32)
    (if (result i32) (ref.is_null (local.get $n)) (then (i32.const 0))
      (else (i32.add (struct.get $Node $value (local.get $n))
        (i32.add (call $gcSum (struct.get $Node $left (local.get $n)))
                 (call $gcSum (struct.get $Node $right (local.get $n))))))))
  (func $linearSum (param $p i32) (result i32)
    (if (result i32) (i32.eqz (local.get $p)) (then (i32.const 0))
      (else (i32.add (i32.load offset=8 (local.get $p))
        (i32.add (call $linearSum (i32.load (local.get $p)))
                 (call $linearSum (i32.load offset=4 (local.get $p))))))))
  ;; $p stays live across both calls, so it is spilled to a frame slot before them and the slot is cleared after.
  (func $spillSum (param $p i32) (result i32) (local $slot i32) (local $left i32)
    (if (i32.eqz (local.get $p)) (then (return (i32.const 0))))
    (global.set $sp (i32.sub (global.get $sp) (i32.const 4)))
    (local.set $slot (global.get $sp))
    (i32.store (local.get $slot) (local.get $p))
    (local.set $left (call $spillSum (i32.load (local.get $p))))
    (local.set $left (i32.add (local.get $left) (call $spillSum (i32.load offset=4 (i32.load (local.get $slot))))))
    (local.set $left (i32.add (local.get $left) (i32.load offset=8 (i32.load (local.get $slot)))))
    (i32.store (local.get $slot) (i32.const 0))
    (global.set $sp (i32.add (local.get $slot) (i32.const 4)))
    (local.get $left))

  (func (export "gcSum") (param $rounds i32) (result i32) (local $i i32) (local $s i32)
    (block $d (loop $l (br_if $d (i32.ge_s (local.get $i) (local.get $rounds)))
      (local.set $s (i32.add (local.get $s) (call $gcSum (global.get $gcRoot))))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $l))) (local.get $s))
  (func (export "linearSum") (param $rounds i32) (result i32) (local $i i32) (local $s i32)
    (block $d (loop $l (br_if $d (i32.ge_s (local.get $i) (local.get $rounds)))
      (local.set $s (i32.add (local.get $s) (call $linearSum (global.get $linearRoot))))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $l))) (local.get $s))
  (func (export "spillSum") (param $rounds i32) (result i32) (local $i i32) (local $s i32)
    (block $d (loop $l (br_if $d (i32.ge_s (local.get $i) (local.get $rounds)))
      (local.set $s (i32.add (local.get $s) (call $spillSum (global.get $linearRoot))))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $l))) (local.get $s)))
