# 0048 · Prove exact classes on the evaluation stack

An ordinary `newobj` that completes pushes an object of exactly the class of
the invoked constructor, even if further derived classes exist. This applies
to C# object initializers such as the original WinForms TableLayoutPanel
initialization. The slot resolver may use that proof to find the original
managed override and retains the existing `callvirt` null check.

The verifier stores the proof separately from the declared CLI stack type.
`dup` preserves it and a control-flow join retains it only when each incoming
path proves the same class. A value-producing instruction clears the proof
unless it supplies its own allocation. Ordinary local, parameter, field and
array loads and call results therefore carry only their declared types.
A sealed declared class remains sufficient for exact slot selection.

This enables the ordinary managed virtual method body without changing
WinForms, its controls or the browser platform boundary. Receivers whose
class is still unknown continue to receive the existing explicit diagnostic.
