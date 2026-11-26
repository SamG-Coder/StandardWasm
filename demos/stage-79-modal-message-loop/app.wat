;; Stage 79: the Oracle's dispatch logic, lowered by hand. A message is (kind << 16) | argument.
;; getMessage blocks: under J it is a JSPI suspending import, under W it waits on a SharedArrayBuffer in a worker.
;; The modal loop in $click is an ordinary nested loop in the handler's own frame, as in WinForms' ShowDialog.
(module
  (import "host" "getMessage" (func $getMessage (result i32)))
  (import "host" "post" (func $post (param i32)))
  (import "host" "setTimer" (func $setTimer (param i32)))
  (import "host" "trace" (func $trace (param i32)))

  ;; Trace ids match TRACE_NAMES in host-core.mjs.
  ;; 0 main-shown, 1 posted-1, 2 click-begin, 3 posted-after-click, 4 posted-during-click, 5 dialog-shown,
  ;; 6 posted-in-dialog, 7 dialog-timer, 8 click-after-dialog, 9 closing, 10 run-returned
  ;; Kinds: 1 posted(trace id), 2 click, 3 dialog shown, 4 timer, 5 close main, 6 quit, 7 echo(n)
  ;; A managed object held on the click handler's frame across the modal loop (Codex review on #179). The dialog's
  ;; timer mutates it through a second reference while the frame is suspended. After ShowDialog returns, the frame's
  ;; reference must still be the same object with the updated state.
  (type $State (struct (field $ticks (mut i32)) (field $tag i32)))
  (global $shared (mut (ref null $State)) (ref.null $State))
  (global $stateCheck (mut i32) (i32.const 0))
  (global $dialogOpen (mut i32) (i32.const 0))
  (global $timerMs (mut i32) (i32.const 10))

  (func $msg (param $kind i32) (param $arg i32) (result i32)
    (i32.or (i32.shl (local.get $kind) (i32.const 16)) (local.get $arg)))

  (func $dispatch (param $m i32) (local $kind i32) (local $arg i32)
    (local.set $kind (i32.shr_u (local.get $m) (i32.const 16)))
    (local.set $arg (i32.and (local.get $m) (i32.const 0xffff)))
    (if (i32.eq (local.get $kind) (i32.const 1)) (then (call $trace (local.get $arg)) (return)))
    (if (i32.eq (local.get $kind) (i32.const 2)) (then (call $click) (return)))
    (if (i32.eq (local.get $kind) (i32.const 3)) (then
      (call $trace (i32.const 5))                                     ;; dialog-shown
      (call $post (call $msg (i32.const 1) (i32.const 6)))            ;; BeginInvoke posted-in-dialog
      (call $setTimer (global.get $timerMs)) (return)))
    (if (i32.eq (local.get $kind) (i32.const 4)) (then
      (call $trace (i32.const 7))                                     ;; dialog-timer, then Close()
      (struct.set $State $ticks (ref.as_non_null (global.get $shared))
        (i32.add (struct.get $State $ticks (ref.as_non_null (global.get $shared))) (i32.const 1)))
      (global.set $dialogOpen (i32.const 0)) (return)))
    (if (i32.eq (local.get $kind) (i32.const 5)) (then
      (call $trace (i32.const 9))                                     ;; closing
      (call $post (call $msg (i32.const 6) (i32.const 0))) (return)))
    (unreachable))

  (func $click (local $state (ref null $State))
    (call $trace (i32.const 2))                                       ;; click-begin
    (local.set $state (struct.new $State (i32.const 0) (i32.const 0x5eed)))
    (global.set $shared (local.get $state))
    (call $post (call $msg (i32.const 1) (i32.const 4)))              ;; BeginInvoke posted-during-click
    ;; ShowDialog: Shown is posted, then a nested loop runs until the dialog closes. The frame of $click stays live.
    (global.set $dialogOpen (i32.const 1))
    (call $post (call $msg (i32.const 3) (i32.const 0)))
    (block $closed (loop $modal
      (br_if $closed (i32.eqz (global.get $dialogOpen)))
      (call $dispatch (call $getMessage))
      (br $modal)))
    ;; Same object (ref.eq), its tag intact and the timer's update visible: 1, otherwise 0.
    (global.set $stateCheck (i32.and (i32.and
      (ref.eq (local.get $state) (global.get $shared))
      (i32.eq (struct.get $State $tag (ref.as_non_null (local.get $state))) (i32.const 0x5eed)))
      (i32.eq (struct.get $State $ticks (ref.as_non_null (local.get $state))) (i32.const 1))))
    (global.set $shared (ref.null $State))
    (call $trace (i32.const 8))                                       ;; click-after-dialog
    (call $post (call $msg (i32.const 5) (i32.const 0))))             ;; BeginInvoke Close

  ;; Application.Run(main) with the main form's Shown handler inlined.
  (func (export "run") (param $timer i32) (local $m i32)
    (global.set $timerMs (local.get $timer))
    (call $trace (i32.const 0))                                       ;; main-shown
    (call $post (call $msg (i32.const 1) (i32.const 1)))              ;; posted-1
    (call $post (call $msg (i32.const 2) (i32.const 0)))              ;; PerformClick
    (call $post (call $msg (i32.const 1) (i32.const 3)))              ;; posted-after-click
    (block $quit (loop $pump
      (local.set $m (call $getMessage))
      (br_if $quit (i32.eq (i32.shr_u (local.get $m) (i32.const 16)) (i32.const 6)))
      (call $dispatch (local.get $m))
      (br $pump)))
    (call $trace (i32.const 10)))                                     ;; run-returned

  (func (export "stateCheck") (result i32) (global.get $stateCheck))

  ;; Latency probe: n round trips. Each blocks in getMessage for a message the host delivers as a separate task.
  (func (export "echo") (param $n i32) (result i32) (local $i i32) (local $sum i32)
    (block $done (loop $next
      (br_if $done (i32.ge_s (local.get $i) (local.get $n)))
      (local.set $sum (i32.add (local.get $sum) (i32.and (call $getMessage) (i32.const 0xffff))))
      (call $post (call $msg (i32.const 7) (local.get $i)))
      (local.set $i (i32.add (local.get $i) (i32.const 1)))
      (br $next)))
    (local.get $sum))
)
