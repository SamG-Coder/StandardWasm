# Stage 314 · Addressable character locals

A verified address-taken Char local shares its packed storage with every forwarded managed alias. Ordinary local reads and writes use the same cell. Frame ownership checks still apply when the method returns a span or can publish an address through caller storage.

The ordinary CLR, Node and Edge fixture covers two independent out locals, repeated aliases to one cell, forwarding, UTF-16 values above 32767, a subsequent ordinary store, and the existing lifetime diagnostic.
