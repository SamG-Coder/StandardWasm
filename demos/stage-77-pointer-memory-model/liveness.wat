;; Stage 77 liveness and reuse fixture for model C (Codex review on #171).
;; A Span over an array outlives the array variable. The payload allocator reuses freed blocks (LIFO free list),
;; and JS frees a payload when FinalizationRegistry reports its wrapper dead. With the owner carried in the Span,
;; the payload must survive. Without it (the ownerless representation first proposed), it is freed and reused.
(module
  (type $Obj (struct (field $ptr i32) (field $len i32)))
  (import "host" "track" (func $track (param (ref $Obj)) (param i32)))
  (memory (export "memory") 1 16384)
  (global $heap (mut i32) (i32.const 65536))
  (global $free (mut i32) (i32.const 0))
  ;; Span locals of the "caller": owner (traced) and address (untraced).
  (global $spanOwner (mut (ref null $Obj)) (ref.null $Obj))
  (global $spanPtr (mut i32) (i32.const 0))

  (func $alloc64 (result i32) (local $p i32)
    (if (global.get $free) (then
      (local.set $p (global.get $free))
      (global.set $free (i32.load (local.get $p)))
      (memory.fill (local.get $p) (i32.const 0) (i32.const 64))
      (return (local.get $p))))
    (local.set $p (global.get $heap))
    (global.set $heap (i32.add (local.get $p) (i32.const 64)))
    (if (i32.gt_u (global.get $heap) (i32.mul (memory.size) (i32.const 65536)))
      (then (drop (memory.grow (i32.const 1)))))
    (local.get $p))
  (func (export "free") (param $p i32)
    (i32.store (local.get $p) (global.get $free))
    (global.set $free (local.get $p)))

  ;; new byte[64] filled with `fill`, registered for reclamation.
  (func $newTracked (param $fill i32) (result (ref $Obj)) (local $o (ref null $Obj))
    (local.set $o (struct.new $Obj (call $alloc64) (i32.const 64)))
    (memory.fill (struct.get $Obj $ptr (local.get $o)) (local.get $fill) (i32.const 64))
    (call $track (ref.as_non_null (local.get $o)) (struct.get $Obj $ptr (local.get $o)))
    (ref.as_non_null (local.get $o)))

  ;; Span<byte> s = new byte[64] { fill... }.AsSpan(8). The array reference itself is not kept.
  (func (export "hold") (param $fill i32) (param $keepOwner i32) (local $o (ref null $Obj))
    (local.set $o (call $newTracked (local.get $fill)))
    (global.set $spanPtr (i32.add (struct.get $Obj $ptr (local.get $o)) (i32.const 8)))
    (global.set $spanOwner (if (result (ref null $Obj)) (local.get $keepOwner)
      (then (local.get $o)) (else (ref.null $Obj)))))
  (func (export "read") (param $i i32) (result i32)
    (i32.load8_u (i32.add (global.get $spanPtr) (local.get $i))))
  (func (export "churn") (param $n i32) (param $fill i32) (local $i i32)
    (block $done (loop $next (br_if $done (i32.ge_s (local.get $i) (local.get $n)))
      (drop (call $newTracked (local.get $fill)))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $next))))
)
