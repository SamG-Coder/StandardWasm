;; Stage 88, model B: every object in linear memory with a precise mark-sweep collector (#196 follow-up).
;; Block header: [size:i32][mark:i32][kind:i32][pad:i32], then the payload. Kinds: 1 = 64 KiB byte array,
;; 2 = list node {next:i32, value:i32}. Roots live on a shadow stack. The collector runs synchronously in the
;; allocation slow path once `budget` bytes have been allocated since the last collection, which is what gives B a
;; hard bound: live data + budget + fragmentation. A long-lived list of `liveNodes` nodes is traced by every collection.
(module
  (import "host" "now" (func $now (result f64)))
  (memory (export "memory") 130 16384)
  ;; Layout: [0, 64 KiB) shadow stack (grows down from 65536); object table at 65536 (2,097,152 slots, 8 MiB);
  ;; heap from 8,454,144. A full table refuses allocation (-1) instead of writing into the heap.
  (global $sp (mut i32) (i32.const 65536))
  (global $table i32 (i32.const 65536))
  (global $tableCount (mut i32) (i32.const 0))
  (global $heap (mut i32) (i32.const 8454144))
  (global $freeArrays (mut i32) (i32.const 0))
  (global $freeNodes (mut i32) (i32.const 0))
  (global $sinceCollect (mut i32) (i32.const 0))
  (global $budget (mut i32) (i32.const 0))
  (global $collections (mut i32) (i32.const 0))
  (global $maxPauseUs (mut f64) (f64.const 0))
  (global $totalPauseUs (mut f64) (f64.const 0))
  (global $rootList (mut i32) (i32.const 0))       ;; global root: head of the long-lived node list

  (func (export "configure") (param $budget i32) (global.set $budget (local.get $budget)))

  (func $bump (param $size i32) (result i32) (local $p i32)
    (local.set $p (global.get $heap))
    (global.set $heap (i32.add (local.get $p) (local.get $size)))
    (if (i32.gt_u (global.get $heap) (i32.mul (memory.size) (i32.const 65536)))
      (then (if (i32.eq (memory.grow (i32.add (i32.shr_u (i32.sub (global.get $heap)
          (i32.mul (memory.size) (i32.const 65536))) (i32.const 16)) (i32.const 1))) (i32.const -1))
        (then (return (i32.const -1))))))
    (local.get $p))

  ;; Allocate a block of the given kind and payload size. Returns the payload address, or -1 when memory is exhausted.
  (func $allocate (param $kind i32) (param $size i32) (result i32) (local $b i32)
    (if (i32.and (i32.ne (global.get $budget) (i32.const 0))
                 (i32.gt_u (global.get $sinceCollect) (global.get $budget)))
      (then (call $collect)))
    (global.set $sinceCollect (i32.add (global.get $sinceCollect) (local.get $size)))
    (if (i32.eq (local.get $kind) (i32.const 1))
      (then (if (global.get $freeArrays) (then
        (local.set $b (global.get $freeArrays)) (global.set $freeArrays (i32.load offset=16 (local.get $b)))))))
    (if (i32.eq (local.get $kind) (i32.const 2))
      (then (if (global.get $freeNodes) (then
        (local.set $b (global.get $freeNodes)) (global.set $freeNodes (i32.load offset=16 (local.get $b)))))))
    (if (i32.eqz (local.get $b)) (then
      (if (i32.ge_u (global.get $tableCount) (i32.const 2097152)) (then (return (i32.const -1))))
      (local.set $b (call $bump (i32.add (local.get $size) (i32.const 16))))
      (if (i32.eq (local.get $b) (i32.const -1)) (then (return (i32.const -1))))
      ;; New blocks are recorded in the object table for the sweep.
      (i32.store (i32.add (global.get $table) (i32.shl (global.get $tableCount) (i32.const 2))) (local.get $b))
      (global.set $tableCount (i32.add (global.get $tableCount) (i32.const 1)))))
    (i32.store (local.get $b) (local.get $size))
    (i32.store offset=4 (local.get $b) (i32.const 0))
    (i32.store offset=8 (local.get $b) (local.get $kind))
    (memory.fill (i32.add (local.get $b) (i32.const 16)) (i32.const 0) (local.get $size))  ;; CLR zero-init
    (i32.add (local.get $b) (i32.const 16)))

  (func $mark (param $payload i32) (local $b i32)
    ;; Iterative over list nodes: mark a node, then follow `next`.
    (block $done (loop $walk
      (br_if $done (i32.eqz (local.get $payload)))
      (local.set $b (i32.sub (local.get $payload) (i32.const 16)))
      (br_if $done (i32.load offset=4 (local.get $b)))                     ;; already marked
      (i32.store offset=4 (local.get $b) (i32.const 1))
      (br_if $done (i32.ne (i32.load offset=8 (local.get $b)) (i32.const 2)))
      (local.set $payload (i32.load (local.get $payload)))                  ;; node.next
      (br $walk))))

  (func $collect (local $start f64) (local $s i32) (local $i i32) (local $b i32) (local $pause f64)
    (local.set $start (call $now))
    ;; Mark: the global root and every shadow-stack slot.
    (call $mark (global.get $rootList))
    (local.set $s (global.get $sp))
    (block $done (loop $roots (br_if $done (i32.ge_u (local.get $s) (i32.const 65536)))
      (call $mark (i32.load (local.get $s)))
      (local.set $s (i32.add (local.get $s) (i32.const 4))) (br $roots)))
    ;; Sweep: unmarked blocks go to their free list; marked blocks are unmarked for the next cycle.
    (block $swept (loop $sweep (br_if $swept (i32.ge_u (local.get $i) (global.get $tableCount)))
      (local.set $b (i32.load (i32.add (global.get $table) (i32.shl (local.get $i) (i32.const 2)))))
      (if (i32.load offset=4 (local.get $b))
        (then (i32.store offset=4 (local.get $b) (i32.const 0)))
        (else (if (i32.ne (i32.load offset=8 (local.get $b)) (i32.const 0)) (then
          (i32.store offset=8 (local.get $b) (i32.const 0))                  ;; kind 0 = free
          (if (i32.eq (i32.load (local.get $b)) (i32.const 65536))
            (then (i32.store offset=16 (local.get $b) (global.get $freeArrays)) (global.set $freeArrays (local.get $b)))
            (else (i32.store offset=16 (local.get $b) (global.get $freeNodes)) (global.set $freeNodes (local.get $b))))))))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $sweep)))
    (global.set $sinceCollect (i32.const 0))
    (global.set $collections (i32.add (global.get $collections) (i32.const 1)))
    (local.set $pause (f64.mul (f64.sub (call $now) (local.get $start)) (f64.const 1000)))
    (global.set $totalPauseUs (f64.add (global.get $totalPauseUs) (local.get $pause)))
    (if (f64.gt (local.get $pause) (global.get $maxPauseUs)) (then (global.set $maxPauseUs (local.get $pause)))))

  ;; Build the long-lived list the collector must trace every cycle. The head is rooted in a global.
  (func (export "buildLive") (param $n i32) (local $i i32) (local $node i32)
    (block $done (loop $next (br_if $done (i32.ge_s (local.get $i) (local.get $n)))
      (local.set $node (call $allocate (i32.const 2) (i32.const 8)))
      (i32.store (local.get $node) (global.get $rootList))
      (i32.store offset=4 (local.get $node) (local.get $i))
      (global.set $rootList (local.get $node))
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $next))))

  ;; Allocate n 64 KiB arrays, each rooted on the shadow stack only while in use (then dropped).
  (func (export "churn") (param $n i32) (result i32) (local $i i32) (local $a i32)
    (global.set $sp (i32.sub (global.get $sp) (i32.const 4)))
    (block $done (loop $next (br_if $done (i32.ge_s (local.get $i) (local.get $n)))
      (local.set $a (call $allocate (i32.const 1) (i32.const 65536)))
      (if (i32.eq (local.get $a) (i32.const -1)) (then
        (global.set $sp (i32.add (global.get $sp) (i32.const 4))) (return (i32.const -1))))
      (i32.store (global.get $sp) (local.get $a))       ;; the frame's reference, visible to the collector
      (i32.store8 (local.get $a) (local.get $i))
      (i32.store (global.get $sp) (i32.const 0))        ;; dropped
      (local.set $i (i32.add (local.get $i) (i32.const 1))) (br $next)))
    (global.set $sp (i32.add (global.get $sp) (i32.const 4)))
    (i32.const 0))

  ;; Sum of the long-lived list, to check it survived every collection intact.
  (func (export "liveSum") (result i32) (local $p i32) (local $sum i32)
    (local.set $p (global.get $rootList))
    (block $done (loop $walk (br_if $done (i32.eqz (local.get $p)))
      (local.set $sum (i32.add (local.get $sum) (i32.load offset=4 (local.get $p))))
      (local.set $p (i32.load (local.get $p))) (br $walk)))
    (local.get $sum))
  (func (export "pages") (result i32) (memory.size))
  (func (export "collections") (result i32) (global.get $collections))
  (func (export "maxPauseUs") (result f64) (global.get $maxPauseUs))
  (func (export "totalPauseUs") (result f64) (global.get $totalPauseUs)))
