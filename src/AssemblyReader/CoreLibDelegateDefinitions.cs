using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    // Stage 181 (issue #256): CoreLib's own field-like event accessors, such as
    // AppDomain.add_ProcessExit, reach System.Delegate.Combine/Remove and
    // Interlocked.CompareExchange<EventHandler> through MethodDef tokens of the same image, not the
    // MemberRefs EventMetadata recognizes in other assemblies. These identify those definitions
    // exactly: the image is the CoreLib definition, and the declaring TypeDef, name and signature
    // match. Anything else returns 0 or false and keeps its existing path.
    internal static class CoreLibDelegateDefinitions
    {
        // EventMetadata.CombineHandler or RemoveHandler for MethodDef row, the static
        // System.Delegate::Combine(Delegate, Delegate) or ::Remove(Delegate, Delegate) returning Delegate.
        internal static int CombineMember(byte[] image, int[] heaps, int[] layout, int row)
        {
            if (!IsCoreLib(image, heaps, layout)) return 0;
            int delegateType = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System", "Delegate");
            if (delegateType <= 0 || ObjectLayouts.Owner(image, heaps, layout, row, false) != delegateType) return 0;
            uint name = (uint)MethodDefinitions.ReadColumn(image, layout, row, 3);
            int member = MatchesAscii(image, heaps, name, "Combine") ? EventMetadata.CombineHandler :
                MatchesAscii(image, heaps, name, "Remove") ? EventMetadata.RemoveHandler : 0;
            if (member == 0) return 0;
            // DEFAULT, two parameters; the result and both parameters are CLASS System.Delegate.
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob <= 0 || blob >= heaps[1]) return 0;
            int limit = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int at = next[0], end = at + size;
            if (size < 5 || size > limit - at || image[at] != 0 || image[at + 1] != 2) return 0;
            at += 2;
            for (int i = 0; i < 3; i++)
            {
                if (at >= end || image[at] != 0x12) return 0;
                int coded = CompressedUnsigned(image, at + 1, end, next);
                if (coded < 0 || (coded & 3) != 0 || coded >> 2 != delegateType) return 0;
                at = next[0];
            }
            return at == end ? member : 0;
        }

        // A TypeDefOrRef coded index naming CoreLib's own System.EventHandler TypeDef.
        internal static bool IsEventHandlerTypeDef(byte[] image, int[] heaps, int[] layout, int coded)
        {
            if ((coded & 3) != 0 || coded >> 2 == 0 || !IsCoreLib(image, heaps, layout)) return false;
            return coded >> 2 == ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System", "EventHandler");
        }

        // A MethodSpec row instantiating CoreLib's own generic
        // System.Threading.Interlocked::CompareExchange<T>(ref T, T, T) at System.EventHandler.
        internal static bool IsHandlerExchange(byte[] image, int[] heaps, int[] layout, int specRow)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return false;
            if (!PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return false;
            var specification = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, specRow, specification) < 0) return false;
            int target = specification[MethodSpecifications.Target];
            if ((target & 1) != 0 || specification[MethodSpecifications.Arity] != 1) return false; // MethodDef only
            int row = target >> 1;
            int interlocked = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System.Threading", "Interlocked");
            if (interlocked <= 0 || ObjectLayouts.Owner(image, heaps, layout, row, false) != interlocked ||
                !MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), "CompareExchange"))
                return false;
            // GENERIC 1, three parameters: !!0 (ref !!0, !!0, !!0). The same bytes EventMetadata checks.
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob <= 0 || blob >= heaps[1]) return false;
            int limit = heaps[0] + heaps[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, next);
            int at = next[0];
            if (size != 12 || size > limit - at || U32(image, (uint)at) != 0x1e030110 ||
                U32(image, (uint)at + 4) != 0x001e1000 || U32(image, (uint)at + 8) != 0x001e001e)
                return false;
            int argument = specification[MethodSpecifications.Arguments], end = specification[MethodSpecifications.End];
            if (argument >= end || image[argument] != 0x12) return false;
            int coded = CompressedUnsigned(image, argument + 1, end, next);
            return next[0] == end && IsEventHandlerTypeDef(image, heaps, layout, coded);
        }

        private static bool IsCoreLib(byte[] image, int[] heaps, int[] layout)
        {
            var counts = new int[45];
            return MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts);
        }
    }
}
