;; Stage 77 memory-pressure probe for model C. Payloads are 64 KiB linear blocks owned by small GC wrappers, so the
;; JS engine's collector sees almost none of the memory in use. Each wrapper is registered with FinalizationRegistry,
;; and freed blocks are reused (LIFO). `pages` reports linear memory size, which never shrinks.
(module
  (type $Obj (struct (field $ptr i32) (field $len i32)))
  (import "host" "track" (func $track (param (ref $Obj)) (param i32)))
  (memory (export "memory") 1 16384)
  (global $heap (mut i32) (i32.const 65536))
  (global $free (mut i32) (i32.const 0))
  (func $alloc (result i32) (local $p i32)
    (if (global.get $free) (then
      (local.set $p (global.get $free)) (global.set $free (i32.load (local.get $p)))
      (memory.fill (local.get $p) (i32.const 0) (i32.const 65536)) (return (local.get $p))))
    (local.set $p (global.get $heap))
    (global.set $heap (i32.add (local.get $p) (i32.const 65536)))
    (if (i32.gt_u (global.get $heap) (i32.mul (memory.size) (i32.const 65536)))
      (then (if (i32.eq (memory.grow (i32.const 1)) (i32.const -1)) (then (return (i32.const -1))))))
    (local.get $p))
  (func (export "free") (param $p i32) (i32.store (local.get $p) (global.get $free)) (global.set $free (local.get $p)))
  ;; Allocate n dropped 64 KiB arrays. Returns -1 when linear memory is exhausted.
  (func (export "churn") (param $n i32) (result i32) (local $i i32) (local $p i32)
    (block $done (loop $next (br_if $done (i32.ge_s (local.get $i) (local.get $n)))
      (local.set $p (call $alloc))
      (if (i32.eq (local.get $p) (i32.const -1)) (then (return (i32.const -1))))
      (call $track (struct.new $Obj (local.get $p) (i32.const 65536)) (local.get $p))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $next)))
    (i32.const 0))
  (func (export "pages") (result i32) (memory.size)))
