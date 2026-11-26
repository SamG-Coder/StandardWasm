# 0076 · Executing assembly identity

The original WinForms reference executable uses `Assembly.GetExecutingAssembly`
to locate a caption embedded in its own published assembly. Its resource call
needs the same assembly descriptor as the compiler's existing reflection path.

Recognize the exact platform `GetExecutingAssembly()` member reference with a
static zero-argument signature returning `System.Reflection.Assembly`. Load
that method body's bundled assembly descriptor from the same canonical table
used by `Type.Assembly` and `Module.Assembly`.

The identity follows the assembly that owns the executing IL. It does not use
the entry image for methods defined in dependencies. The compiler preserves
this ownership across linking and emits the same descriptor for repeated calls.
The stage covers the referenced platform method. Other stack-introspection
methods and CLR native runtime implementation bodies retain their existing
linking behavior.
