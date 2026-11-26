# 0055 · Bind internal methods across friend assemblies

Status: proposed (#280). Extends decision 0028, which established the friend identity rules for
type resolution.

## Context

The unchanged x86 WinForms bundle reaches `Application.EnableVisualStyles`. Its call at IL_0016
targets `Windows.Win32.PInvoke.GetModuleHandle(string)` in `System.Windows.Forms.Primitives`. That
is an `internal` managed wrapper with an IL body. `System.Windows.Forms` may call it because
Primitives names it in a signed `InternalsVisibleTo` grant. The metadata readers already resolve
the declaring type and see the grant, but the cross-assembly MemberRef candidate loop in
`AssemblyBundle.Resolve` only admitted public methods (and family methods for base calls), so the
call failed with -6.

## Decision

When a method candidate belongs to another bundle assembly, these access levels are admitted if
`FriendAssemblies.Allows(declaring assembly, caller)` grants the caller:

- Assembly (`internal`)
- FamORAssem (`protected internal`), through its assembly part

The friend check uses decision 0028's identity rules unchanged. Unsigned grants match the simple
name. Signed grants also match the full public key. The check is evaluated once per resolution.

Unchanged:

- Private and FamANDAssem (`private protected`) are rejected.
- Family-only (`protected`) is admitted only through the existing base-call rule.
- Signature, static-ness, constructor, virtual and ambiguity checks still apply.
- A refused call keeps the existing -6 diagnostic at its call, with the output buffer untouched.

Fields are out of scope. Friend access to `internal` fields is a separate change.

## Verification

Stage 127 builds ordinary netstandard2.0 providers and callers with fixed assembly identities.
The CLR loads each caller with the provider under test in its own `AssemblyLoadContext` and
enforces `InternalsVisibleTo` itself. The self-hosted compiler reproduces every outcome in Node
and Edge:

- Unsigned and signed grants return the CLR's values for an `internal static` call, a
  `protected internal static` call from a non-subclass, and an `internal` constructor plus an
  `internal` instance method.
- A missing grant, and a caller signed with the wrong key, are refused by the CLR with
  MethodAccessException. They return -6 at the same call.

Ignoring the grant result makes the missing-grant case compile, so the fixture pins the check.
