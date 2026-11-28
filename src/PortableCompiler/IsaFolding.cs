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
        internal static void Lower(byte[] image, int[] layout, int[] heaps, int[] records, int[] clauses)
        {
            bool changed = false;
            for (int slot = 0; slot < records.Length; slot += Width)
            {
                int kind = records[slot];
                bool direct = kind == Call && records[slot + 3] == 0 || kind == MemberCall && records[slot + 3] == 0x28;
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
            if (changed && clauses.Length == 0) RuntimeTypeBranches.RemoveUnreachable(records);
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
