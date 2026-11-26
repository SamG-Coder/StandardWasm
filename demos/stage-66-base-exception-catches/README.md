# Stage 66 · Managed exception base catches

Run `npm run demo -- stage-66-base-exception-catches`. The generated module
returns 118 after rethrowing a division exception through a finally block.

The CLR, Node and Edge comparisons cover ordered specific and base
catches, normal returns, and base catches across several existing exception
tags. Only platform `System.Exception` and `System.SystemException` catch
types are recognized here. Their handlers still discard the exception operand.
WebAssembly traps are outside managed exception dispatch.
