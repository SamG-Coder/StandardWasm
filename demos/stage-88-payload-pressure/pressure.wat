;; Stage 88: bounding linear-memory growth for model C payloads (decision 0025 follow-up, #196).
;; Each allocation is a 64 KiB linear payload owned by a GC wrapper registered with FinalizationRegistry. Freed
;; blocks are reused (LIFO). Two strategies can be switched on:
;;   ballast: the wrapper also holds a GC byte array of payload >> ballastShift bytes, so the engine's own heap growth
;;            tracks payload volume and its collector runs sooner;
;;   budget:  when unreclaimed payload bytes exceed budgetBytes, the allocation slow path calls the suspending
;;            import `relieve` (JSPI, decision 0026), which yields to the event loop so finalization can run.
(module
  (type $Ballast (array (mut i8)))
  (type $Obj (struct (field $ptr i32) (field $len i32) (field $ballast (ref null $Ballast))))
  (import "host" "track" (func $track (param (ref $Obj)) (param i32)))
  (import "host" "relieve" (func $relieve (result i32)))
  (memory (export "memory") 1 16384)
  (global $heap (mut i32) (i32.const 65536))
  (global $free (mut i32) (i32.const 0))
  (global $live (mut i32) (i32.const 0))          ;; payload bytes allocated and not yet reclaimed
  (global $ballastShift (mut i32) (i32.const 0))  ;; 0 = no ballast
  (global $budget (mut i32) (i32.const 0))        ;; 0 = no budget
  (global $relieveCalls (mut i32) (i32.const 0))
  (global $relieveFailures (mut i32) (i32.const 0))

  (func (export "configure") (param $shift i32) (param $budget i32)
    (global.set $ballastShift (local.get $shift)) (global.set $budget (local.get $budget)))

  (func $alloc (result i32) (local $p i32)
    ;; Slow path: over budget, let the host run finalization before taking more memory.
    (if (i32.and (i32.ne (global.get $budget) (i32.const 0)) (i32.gt_u (global.get $live) (global.get $budget)))
      (then
        (global.set $relieveCalls (i32.add (global.get $relieveCalls) (i32.const 1)))
        (if (i32.eqz (call $relieve))
          (then (global.set $relieveFailures (i32.add (global.get $relieveFailures) (i32.const 1)))))))
    (global.set $live (i32.add (global.get $live) (i32.const 65536)))
    (if (global.get $free) (then
      (local.set $p (global.get $free)) (global.set $free (i32.load (local.get $p)))
      (memory.fill (local.get $p) (i32.const 0) (i32.const 65536)) (return (local.get $p))))
    (local.set $p (global.get $heap))
    (global.set $heap (i32.add (local.get $p) (i32.const 65536)))
    (if (i32.gt_u (global.get $heap) (i32.mul (memory.size) (i32.const 65536)))
      (then (if (i32.eq (memory.grow (i32.const 1)) (i32.const -1)) (then (return (i32.const -1))))))
    (local.get $p))

  (func (export "free") (param $p i32)
    (global.set $live (i32.sub (global.get $live) (i32.const 65536)))
    (i32.store (local.get $p) (global.get $free)) (global.set $free (local.get $p)))

  ;; Allocate n dropped 64 KiB arrays in one call. Returns -1 when linear memory is exhausted.
  (func (export "churn") (param $n i32) (result i32) (local $i i32) (local $p i32)
    (block $done (loop $next (br_if $done (i32.ge_s (local.get $i) (local.get $n)))
      (local.set $p (call $alloc))
      (if (i32.eq (local.get $p) (i32.const -1)) (then (return (i32.const -1))))
      (call $track
        (struct.new $Obj (local.get $p) (i32.const 65536)
          (if (result (ref null $Ballast)) (global.get $ballastShift)
            (then (array.new_default $Ballast (i32.shr_u (i32.const 65536) (global.get $ballastShift))))
            (else (ref.null $Ballast))))
        (local.get $p))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $next)))
    (i32.const 0))

  (func (export "live") (result i32) (global.get $live))
  (func (export "pages") (result i32) (memory.size))
  (func (export "relieveCalls") (result i32) (global.get $relieveCalls))
  (func (export "relieveFailures") (result i32) (global.get $relieveFailures)))
