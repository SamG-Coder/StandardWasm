using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;

namespace PortableCompiler
{
    // Decision 0104: hardware intrinsic feature queries are constant false on this target.
    //
    //   System.Runtime.Intrinsics.{X86,Arm,Wasm}.*::get_IsSupported()   (including nested X64/Arm64)
    //   System.Runtime.Intrinsics.Vector64/128/256/512::get_IsHardwareAccelerated()
    //   System.Numerics.Vector::get_IsHardwareAccelerated()
    //
    // .NET reports exactly this on a target without those instruction sets, and every caller must
    // keep a software path for it. Each query becomes `ldc.i4.0`. A brtrue/brfalse that consumes
    // it directly becomes a nop or an unconditional branch, and code no longer reachable from
    // the entry is removed before classification, so the accelerated paths (Vector512 loads,
    // Bmi2 pointer outputs, prefetches) are never lowered. Methods with exception clauses keep
    // their dead code; only the constant and the branch are folded there.
    internal static class IsaFolding
    {
        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records, int[] clauses, int parameters = int.MaxValue)
        {
            bool changed = false;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                bool direct = kind == Call && records[slot + 3] == 0 || kind == MemberCall && records[slot + 3] == 0x28;
                if (direct && kind == MemberCall && UnsupportedMember(image, layout, heaps, records[slot + 1], records, slot)) continue;
                if (!direct || !IsFeatureQuery(image, layout, heaps, records[slot + 1], kind == MemberCall)) continue;
                records[slot] = Constant;
                records[slot + 1] = 0;
                records[slot + 3] = 0;
                int branch = records[slot + 2] * Width;
                if (branch >= records.Length || records[branch] != Conditional ||
                    ValueOperands.Entry(records, branch / Width, clauses)) continue;
                int comparison = records[branch + 3];
                if (comparison != 0 && comparison != 0x45) continue; // brtrue / brfalse only
                records[slot] = Nop;
                records[branch] = comparison == 0x45 ? Branch : Nop;
                records[branch + 3] = 0;
                changed = true;
            }
            if (changed && clauses.Length == 0)
            {
                RuntimeTypeBranches.RemoveUnreachable(records);
                // Decision 0105: `bool b = Avx512F.IsSupported && Bmi2.IsSupported; if (b) ...` stores the
                // folded false into a local. A local whose every store is of that constant 0 and that is
                // never addressed reads as 0 everywhere (CLI locals start zero-initialized too), so its
                // loads fold, and the branches on them resolve in turn.
                while (FoldZeroLocals(records, clauses, parameters)) RuntimeTypeBranches.RemoveUnreachable(records);
            }
        }

        private static bool FoldZeroLocals(int[] records, int[] clauses, int parameters)
        {
            bool changed = false;
            for (int store = 0; store < records.Length; store += Width)
            {
                if (records[store] != Store || records[store + 1] < parameters) continue;
                int local = records[store + 1];
                if (!OnlyZeroStores(records, local)) continue;
                for (int load = 0; load < records.Length; load += Width)
                {
                    if (records[load] != Load || records[load + 1] != local) continue;
                    int branch = records[load + 2] * Width;
                    if (branch >= records.Length || records[branch] != Conditional ||
                        ValueOperands.Entry(records, branch / Width, clauses)) continue;
                    int comparison = records[branch + 3];
                    if (comparison != 0 && comparison != 0x45) continue;
                    // ldloc b (always 0); brtrue -> fall through, brfalse -> branch
                    records[load] = Nop;
                    records[load + 1] = 0;
                    records[branch] = comparison == 0x45 ? Branch : Nop;
                    records[branch + 3] = 0;
                    changed = true;
                }
            }
            return changed;
        }

        // Every live store of `local` receives `ldc.i4.0` as its only possible value, and no
        // instruction takes the local's address.
        private static bool OnlyZeroStores(int[] records, int local)
        {
            for (int at = 0; at < records.Length; at += Width)
            {
                int kind = records[at];
                if (kind == FrameAddress && records[at + 1] == local) return false;
                if (kind != Store || records[at + 1] != local) continue;
                int index = at / Width;
                for (int from = 0; from < records.Length; from += Width)
                {
                    int source = records[from];
                    if (source == 0) continue;
                    bool falls = source != Branch && source != Return && source != ThrowNull && source != Rethrow &&
                        source != Leave && source != EndFinally && records[from + 2] == index;
                    bool jumps = (source == Branch || source == Conditional || source == Leave) && records[from + 1] == index;
                    if (source == Switch)
                        for (int i = 0; i < records[from + 3]; i++) jumps |= SwitchTarget(records, records[from + 1], i) == index;
                    if (jumps) return false;
                    if (falls && (source != Constant || records[from + 1] != 0)) return false;
                }
            }
            return true;
        }

        // Decision 0105: any other static member of an X86/Arm/Wasm intrinsic class. Its IsSupported is
        // false here, so the CLR raises PlatformNotSupportedException if it runs; callers guard it with
        // that query, so the call is normally pruned above. A call that remains keeps its signature for
        // verification and traps (`unreachable`) instead of raising the catchable exception. The record
        // becomes PlatformUnsupported: operand = parameter count, aux = result stack type (or Void).
        private static bool UnsupportedMember(byte[] image, int[] layout, int[] heaps, int row, int[] records, int slot)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return false;
            var reference = new int[AssemblyReferences.SymbolWidth];
            if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0) return false;
            int assembly = reference[AssemblyReferences.AssemblyRow];
            if (!PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, layout, counts, assembly) &&
                !PlatformAssemblyIdentity.IsFrameworkAssembly(image, heaps, layout, counts, assembly, "System.Runtime.Intrinsics"))
                return false;
            uint space = (uint)reference[3];
            if (reference[AssemblyReferences.Depth] > 1 || MatchesAscii(image, heaps, (uint)reference[4], "get_IsSupported") ||
                !MatchesAscii(image, heaps, space, "System.Runtime.Intrinsics.X86") &&
                !MatchesAscii(image, heaps, space, "System.Runtime.Intrinsics.Arm") &&
                !MatchesAscii(image, heaps, space, "System.Runtime.Intrinsics.Wasm")) return false;
            int blob = reference[5];
            if (blob < 1 || blob >= heaps[1]) return false;
            var next = new int[1];
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int at = next[0], end = at + size;
            if (size < 3 || size > limit - at || image[at] != 0x00) return false; // static, not generic
            int parameters = image[at + 1];
            int result = ResultType(image, layout, heaps, at + 2, end);
            if (result < 0) return false;
            records[slot] = PlatformUnsupported;
            records[slot + 1] = parameters;
            records[slot + 3] = result;
            return true;
        }

        // The stack type of a primitive, pointer or vector return type, Void, or -1.
        private static int ResultType(byte[] image, int[] layout, int[] heaps, int at, int end)
        {
            int element = image[at];
            if (element == 0x01) return SignatureTypes.Void;
            if (element >= 0x02 && element <= 0x09) return SignatureTypes.I32;
            if (element == 0x0a || element == 0x0b) return SignatureTypes.I64;
            if (element == 0x0c) return SignatureTypes.F32;
            if (element == 0x0d) return SignatureTypes.F64;
            if (element == 0x18 || element == 0x19 || element == 0x0f) return SignatureTypes.Native;
            var next = new int[1];
            int tag = SimdVectors.ReadSignatureTag(image, layout, heaps, at, end, next);
            return SimdVectors.IsVector(tag) ? tag : -1;
        }

        private static bool IsFeatureQuery(byte[] image, int[] layout, int[] heaps, int row, bool member)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            if (first < 0) return false;
            uint name, space, typeName;
            if (member)
            {
                var reference = new int[AssemblyReferences.SymbolWidth];
                if (AssemblyReferences.ReadMember(image, heaps, layout, row, reference) < 0) return false;
                // The type slots name the outermost type, which carries the namespace.
                int assembly = reference[AssemblyReferences.AssemblyRow];
                if (!PlatformAssemblyIdentity.IsPlatformAssembly(image, heaps, layout, counts, assembly) &&
                    !PlatformAssemblyIdentity.IsFrameworkAssembly(image, heaps, layout, counts, assembly, "System.Runtime.Intrinsics") &&
                    !PlatformAssemblyIdentity.IsFrameworkAssembly(image, heaps, layout, counts, assembly, "System.Numerics.Vectors"))
                    return false;
                name = (uint)reference[4];
                typeName = (uint)reference[2];
                space = (uint)reference[3];
                if (reference[AssemblyReferences.Depth] > 1) return false;
                return Matches(image, heaps, name, typeName, space, reference[AssemblyReferences.Depth] != 0);
            }
            if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
            if (row < 1 || row > counts[6]) return false;
            name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            int type = ObjectLayouts.Owner(image, heaps, layout, row, false);
            if (type < 1) return false;
            int outer = NestedTypes.Enclosing(image, heaps, layout, counts, type);
            if (outer < 0) return false;
            bool nested = outer != 0;
            if (nested && NestedTypes.Enclosing(image, heaps, layout, counts, outer) != 0) return false;
            var names = new int[2];
            if (ObjectLayouts.TypeDefinitionNames(image, heaps, layout, nested ? outer : type, names) < 0) return false;
            return Matches(image, heaps, name, (uint)names[0], (uint)names[1], nested);
        }

        private static bool Matches(byte[] image, int[] heaps, uint name, uint typeName, uint space, bool nested)
        {
            if (MatchesAscii(image, heaps, name, "get_IsSupported"))
                return MatchesAscii(image, heaps, space, "System.Runtime.Intrinsics.X86") ||
                    MatchesAscii(image, heaps, space, "System.Runtime.Intrinsics.Arm") ||
                    MatchesAscii(image, heaps, space, "System.Runtime.Intrinsics.Wasm");
            if (nested || !MatchesAscii(image, heaps, name, "get_IsHardwareAccelerated")) return false;
            if (MatchesAscii(image, heaps, space, "System.Numerics")) return MatchesAscii(image, heaps, typeName, "Vector");
            return MatchesAscii(image, heaps, space, "System.Runtime.Intrinsics") &&
                (MatchesAscii(image, heaps, typeName, "Vector64") || MatchesAscii(image, heaps, typeName, "Vector128") ||
                 MatchesAscii(image, heaps, typeName, "Vector256") || MatchesAscii(image, heaps, typeName, "Vector512"));
        }
    }
}
