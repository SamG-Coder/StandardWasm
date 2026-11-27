using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ScopedValuePins
    {
        internal const int FrameFlag = 8;

        internal static void Lower(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int method, int methods, int localBlob, int[] records, int[] clauses, int[] frame, int parameters,
            int locals, int[] objects, NativeAggregatePlans plans)
        {
            if (parameters == 0 || locals != 1 || localBlob <= 0 || clauses.Length != 0 ||
                !IsValueLocation(frame[0]) || frame[parameters] != Native ||
                MethodDefinitions.ReadColumn(image, layout, method, 1) != 0 ||
                (MethodDefinitions.ReadColumn(image, layout, method, 2) & 0x10) == 0) return;
            int type = LocatedValue(frame[0]);
            if (!MatchesLocal(bundle, image, layout, heaps, owner, localBlob, objects, type)) return;

            int pin = Next(records, 0), load = Next(records, pin), convert = Next(records, load);
            if (!At(records, 0, Load, 0) || !At(records, pin, Store, parameters) ||
                !At(records, load, Load, parameters) || !At(records, convert, ConvertInteger, 0xe0)) return;
            int call = Next(records, convert);
            for (int argument = 1; argument < parameters; argument++)
            {
                if (!At(records, call, Load, argument)) return;
                call = Next(records, call);
            }
            if (!At(records, call, Call, -1) || records[call + 3] != 0) return;
            int after = Next(records, call);
            if (!At(records, after, Return, -1) || Next(records, after) != records.Length) return;

            int target = records[call + 1];
            int byrefSignature = MethodDefinitions.ReadColumn(image, layout, method, 4);
            if (target < 1 || target > methods ||
                MethodDefinitions.ReadColumn(image, layout, target, 1) != 0 ||
                (MethodDefinitions.ReadColumn(image, layout, target, 2) & 0x10) == 0 ||
                ObjectLayouts.Owner(image, heaps, layout, target, false) != ObjectLayouts.Owner(image, heaps, layout, method, false) ||
                !MatchesPointerSignature(image, heaps, byrefSignature, MethodDefinitions.ReadColumn(image, layout, target, 4),
                    parameters)) return;
            if (!DirectWin32Borrow(bundle, image, layout, heaps, owner, target, methods, parameters, type, objects) &&
                (ObjectLayouts.AssemblyOf(objects, type) != owner ||
                    !ManagedFieldBorrow(image, layout, heaps, target, parameters, type, objects, frame, byrefSignature))) return;

            int plan = NativeAggregates.Plan(bundle, objects, type, plans);
            if (plan <= 0 || NativeAggregates.Size(plans.Plans, plan) <= 0) return;
            // The existing native local and original pointer call survive.
            // Only the exact pinned store acquires a scoped native address.
            records[pin] = PinScopedValue;
            records[pin + 3] = plan;
        }

        private static bool MatchesLocal(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int blob, int[] objects, int type)
        {
            int limit = heaps[0] + heaps[1];
            var cursor = new int[1];
            if (blob >= heaps[1]) return false;
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, cursor), start = cursor[0];
            if (size < 6 || size > limit - start || U32(image, (uint)start) != 0x10450107 ||
                image[start + 4] != 0x11) return false; // one local, pinned ref valuetype
            int end = start + size;
            int coded = CompressedUnsigned(image, start + 5, end, cursor);
            if (coded < 1 || cursor[0] != end) return false;
            var identity = new int[2];
            return AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, identity) >= 0 &&
                identity[0] == ObjectLayouts.AssemblyOf(objects, type) && identity[1] == ObjectLayouts.TypeDef(objects, type);
        }

        private static bool MatchesPointerSignature(byte[] image, int[] heaps, int byref, int pointer, int arity)
        {
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, byref, arity, cursor), start = cursor[0];
            int first = SignatureExtents.Type(image, start, end);
            int otherEnd = SignatureReader.ReadStatic(image, heaps, pointer, arity, cursor), other = cursor[0];
            if (first < 0 || first >= end || otherEnd < 0 || end - start != otherEnd - other) return false;
            int replacement = first - start;
            if (image[first] != 0x10 || image[other + replacement] != 0x0f) return false;
            for (int i = 0; i < end - start; i++)
                if (i != replacement && image[start + i] != image[other + i]) return false;
            return true;
        }

        private static bool ManagedFieldBorrow(byte[] image, int[] layout, int[] heaps, int method,
            int parameters, int type, int[] objects, int[] frame, int signature)
        {
            for (int i = 1; i < parameters; i++)
                if (!IsInteger(frame[i])) return false;
            var cursor = new int[1];
            int end = SignatureReader.ReadStatic(image, heaps, signature, parameters, cursor);
            if (end < 0) return false;
            int result = image[cursor[0]];
            if (result != 1 && (result < 2 || result > 0x0b)) return false;
            int[] body = DecodeLeaf(image, layout, method, parameters);
            if (body == null) return false;

            // A small abstract stack distinguishes field/scalar data from the
            // borrowed pointer. Only exact field accesses may consume it.
            var borrowed = new bool[32];
            int depth = 0;
            for (int at = 0; at < body.Length; at = Next(body, at))
            {
                int kind = body[at], operand = body[at + 1];
                if (kind == Return)
                    return Next(body, at) == body.Length && depth == (result == 1 ? 0 : 1) &&
                        (depth == 0 || !borrowed[0]);
                if (kind == Load || kind == Constant || kind == WideConstant || kind == Dup)
                {
                    if (depth == borrowed.Length || kind == Dup && depth == 0) return false;
                    borrowed[depth] = kind == Load && operand == 0 || kind == Dup && borrowed[depth - 1];
                    depth++;
                }
                else if (kind == GetField || kind == SetField)
                {
                    int receiver = depth - (kind == GetField ? 1 : 2);
                    if (receiver < 0 || !borrowed[receiver] || body[at + 3] != 0 ||
                        kind == SetField && borrowed[depth - 1] ||
                        ObjectLayouts.Owner(image, heaps, layout, operand, true) != ObjectLayouts.TypeDef(objects, type) ||
                        !IsInteger(ObjectLayouts.FieldType(objects, type, operand))) return false;
                    depth = receiver;
                    if (kind == GetField) borrowed[depth++] = false;
                }
                else if (kind == Binary && (operand >= 0x6a && operand <= 0x6c ||
                    operand >= 0x71 && operand <= 0x73 || operand == 0x46))
                {
                    if (depth < 2 || borrowed[depth - 1] || borrowed[depth - 2]) return false;
                    depth--;
                }
                else if (kind != Nop) return false;
            }
            return false;
        }

        private static bool DirectWin32Borrow(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner, int method, int methods,
            int parameters, int type, int[] objects)
        {
            if (parameters != 4 && parameters != 5) return false;
            int[] body = DecodeLeaf(image, layout, method, parameters);
            if (body == null || !At(body, 0, Constant, 0)) return false;
            int at = Next(body, 0);
            if (!ErrorCall(image, layout, heaps, body, at, MarshalErrorMetadata.SetSystem)) return false;
            at = Next(body, at);
            for (int argument = 0; argument < parameters; argument++)
            {
                if (!At(body, at, Load, argument)) return false;
                at = Next(body, at);
            }
            if (!At(body, at, Call, -1) || body[at + 3] != 0) return false;
            int leaf = body[at + 1];
            if (leaf < 1 || leaf > methods ||
                ObjectLayouts.Owner(image, heaps, layout, leaf, false) != ObjectLayouts.Owner(image, heaps, layout, method, false) ||
                (MethodDefinitions.ReadColumn(image, layout, leaf, 2) & 0x2010) != 0x2010 ||
                MethodDefinitions.ReadColumn(image, layout, leaf, 1) != 0x80 ||
                MethodDefinitions.ReadColumn(image, layout, leaf, 4) != MethodDefinitions.ReadColumn(image, layout, method, 4))
                return false;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                !RectAbi(bundle, image, layout, heaps, owner, leaf, parameters, objects, type) ||
                NativeImports.Find(image, heaps, layout, counts, "USER32.dll",
                    parameters == 5 ? "AdjustWindowRectExForDpi" : "AdjustWindowRectEx", leaf, 0x101) != leaf) return false;
            at = Next(body, at);
            if (!ErrorCall(image, layout, heaps, body, at, MarshalErrorMetadata.GetSystem)) return false;
            at = Next(body, at);
            if (!ErrorCall(image, layout, heaps, body, at, MarshalErrorMetadata.SetPInvoke)) return false;
            at = Next(body, at);
            return At(body, at, Return, -1) && Next(body, at) == body.Length;
        }

        private static bool RectAbi(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner, int row,
            int parameters, int[] objects, int type)
        {
            // The native leaf must operate on exactly four contiguous words,
            // with no secondary pointer, managed reference, or callback argument.
            var cursor = new int[1];
            int signature = MethodDefinitions.ReadColumn(image, layout, row, 4);
            int end = SignatureReader.ReadStatic(image, heaps, signature, parameters, cursor);
            const string foundation = "Windows.Win32.Foundation";
            const string windows = "Windows.Win32.UI.WindowsAndMessaging";
            if (end < 0 ||
                !NamedValue(bundle, image, layout, heaps, owner, cursor, end, foundation, "BOOL") ||
                !SignatureReader.TakeElement(image, cursor, end, 0x0f) ||
                !NamedValue(bundle, image, layout, heaps, owner, cursor, end, foundation, "RECT") ||
                !NamedValue(bundle, image, layout, heaps, owner, cursor, end, windows, "WINDOW_STYLE") ||
                !NamedValue(bundle, image, layout, heaps, owner, cursor, end, foundation, "BOOL") ||
                !NamedValue(bundle, image, layout, heaps, owner, cursor, end, windows, "WINDOW_EX_STYLE") ||
                parameters == 5 && !SignatureReader.TakeElement(image, cursor, end, 9) || cursor[0] != end) return false;
            var size = new int[2];
            return NativeValueLayouts.Measure(bundle, ObjectLayouts.AssemblyOf(objects, type),
                ObjectLayouts.TypeDef(objects, type), size) == 0 && size[0] == 16 && size[1] == 4;
        }

        private static bool NamedValue(BundleImages bundle, byte[] image, int[] layout, int[] heaps, int owner,
            int[] cursor, int end, string space, string name)
        {
            if (!SignatureReader.TakeElement(image, cursor, end, 0x11)) return false;
            int coded = CompressedUnsigned(image, cursor[0], end, cursor);
            var identity = new int[2];
            if (coded < 1 || AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, identity) < 0)
                return false;
            return NamedValueDefinition(bundle, identity[0], identity[1], space, name);
        }

        private static bool NamedValueDefinition(BundleImages bundle, int owner, int row, string space, string name)
        {
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return false;
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && ObjectLayouts.NamedDefinition(image, heaps, layout, counts, first, row,
                space, name);
        }

        private static bool ErrorCall(byte[] image, int[] layout, int[] heaps, int[] body, int at, int member) =>
            At(body, at, MemberCall, -1) && body[at + 3] == 0x28 &&
            MarshalErrorMetadata.Identify(image, heaps, layout, body[at + 1]) == member;

        private static int[] DecodeLeaf(byte[] image, int[] layout, int row, int parameters)
        {
            var body = new int[6];
            uint rva = unchecked((uint)MethodDefinitions.ReadColumn(image, layout, row, 0));
            if (MethodBodies.Read(image, rva, body) != 0 || body[1] < 1 || body[1] > 256 ||
                body[2] > 32 || body[3] != 0 || body[5] != 0) return null;
            var records = new int[body[1] * Width];
            return Decode(image, body[0], body[1], parameters, 0, records, new int[0], false) == 0 ? records : null;
        }

        private static int Next(int[] records, int at) =>
            at >= 0 && at < records.Length && records[at + 2] * Width > at ? records[at + 2] * Width : -1;

        private static bool At(int[] records, int at, int kind, int operand) =>
            at >= 0 && at < records.Length && records[at] == kind && (operand < 0 || records[at + 1] == operand);
    }
}
