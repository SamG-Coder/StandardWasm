# 0001 · Organize demos by compiler capability

Each runnable demo has a stable numbered directory under demos/. Its project, source, invocation and expected result travel together. Both the standalone runner and the verification suite discover the same manifest. Keep the compiler and the independent CLR oracle outside the demo tree so a demo change can be reviewed separately from lowering or reference behavior.

Stage numbers identify reproducible examples. GOAL.md tracks larger milestones, which can require multiple demos. Coordinate new stage numbers and cross-cutting interfaces on the issue before parallel implementation.
