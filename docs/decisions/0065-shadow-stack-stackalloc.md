# 0065 · stackalloc on a shadow stack in memory 0

Status is proposed in #275. It builds on decision 0060 (primitive pointers in
memory 0). Codex asked for this slice (handoff 081). The span representation
(decision 0025's `(owner, address)`) and managed byrefs (#271, #273) stay
separate. A stackalloc span can later share this stack, with a null owner.

## Region

Wasm locals have no address, so `localloc` needs linear memory.
- The shadow stack is a module-owned region of 16 pages (1 MiB). A frame
  that allocates takes it with `memory.grow` the first time it runs, so it
  never overlaps pages the host owns, such as Stage 131's resources.
- Three i32 globals record it: `$sp`, `base` and `limit`. `$sp` starts at
  `limit` and grows down. Zero means not yet reserved.
- Exhaustion, or a failed `memory.grow`, is a Wasm engine trap. The CLR
  raises `StackOverflowException` there, which ends the process. It is never
  a typed managed exception here.

## Allocation

`localloc` pops a native unsigned size (an int32 is accepted) and pushes the
block's address.
- The block is 16-byte aligned and zero-filled. ECMA-335 III.3.47 requires
  zeroing when InitLocals is set. Without it the contents are unspecified, and
  zeroing is a valid refinement (decision 0051).
- C# scales element counts with `mul.ovf.un` on native ints. So checked native
  `add`, `sub` and `mul` (signed and unsigned) are supported at the 4-byte
  wasm32 width, raising the typed `OverflowException` as the 32-bit CLR does.

## Frame lifetime

A function that contains `localloc` saves `$sp` in a local on entry.
- It restores `$sp` before every `ret`.
- An outer `try_table (catch_all_ref)` restores it when an exception leaves the
  frame, and then `throw_ref`s the same exception.
- Such frames never tail-call. ECMA-335 forbids `tail.` from a frame that used
  `localloc`.
- Every host export enters through a wrapper, the same wrapper as decision
  0057's export boundary. If the stack is reserved, it resets `$sp` to `limit`.
  A raw engine trap can skip a frame's release, and the host never re-enters an
  export while managed frames are live.

## Still diagnosed

- `localloc` inside a type initializer, which already has its own guard wrapper
- `Span<T>` over `stackalloc`
- `fixed`, `calli`, `sizeof` and struct pointers
