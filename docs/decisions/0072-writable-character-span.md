# 0072 · Writable character spans over managed arrays

A `Span<char>` made from a managed `char[]` retains the array as its traced owner. It carries a start and length in the descriptor used by readonly spans. The compiler has distinct stack types for writable and readonly views, and accepts conversion from writable to readonly through the platform operator. The conversion keeps the same owner.

The platform indexer checks the logical length before producing a traced char location. The location can be copied to a local or passed to a managed callee. Its indirect load reads an unsigned UTF-16 code unit and its indirect store truncates to the packed i16 representation. Native indirect operations retain their existing linear-memory behavior. A module that uses only traced locations needs no native memory import.

The ordinary array constructor handles null as the empty default. Borrowed native or stack storage, range constructors, slicing and arbitrary ref-like aggregate fields need separate lifetime and validation work. Readonly string views have no writable constructor or conversion.
