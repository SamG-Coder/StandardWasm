# Stage 311 · Readonly character location forwarding

Verified managed calls can pass a readonly character location through direct callees and local aliases. The original code retains its Unicode behavior. Character offsets stay within the managed owner and the compiler diagnoses writes and escaped readonly references.

The ordinary CLR, Node and Edge fixture covers forwarding through branches, forward and backward offsets, an empty reference and a writable control.
