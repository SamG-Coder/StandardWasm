# Stage 308 · Proven closed interface casts

An original IL castclass can use the managed stack's existing assignability proof for a closed interface. A class that already implements the exact interface preserves its reference identity through the cast. Bases, derived classes and null use the ordinary CLI rules.

The verification cases compare Windows x86 CLR, Node and Edge. The generic-argument mismatch and widened object cases retain explicit compiler diagnostics while the runtime interface check is unavailable for those profiles.
