# Stage 355 · Same-queue window sends

The original `USER32!SendMessageTimeoutW` binding calls the guest window procedure synchronously on the browser host's single GUI queue. It preserves the distinction between the success status and the optional pointer-sized message result. The Windows x86 oracle exercises the same cases with a registered window procedure, including nested sends, a handler that exceeds the supplied timeout, an inner error change, invalid handles, and destruction within a handler.

```sh
npm run demo -- stage-355-send-message-timeout --no-rebuild
```

The Node and Edge runner also checks the result buffer bounds and growth during dispatch. The host currently supports same-queue delivery. Broadcast and cross-thread deadline behavior remain separate work.
