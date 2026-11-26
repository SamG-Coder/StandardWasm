using PeLibrary;
using static PeLibrary.ImageBytes;

namespace PortableCompiler
{
    internal static class BundleConstraints
    {
        internal static int Check(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int[] objects, int[] arguments, int[] table,
            bool method = false, int context = 0)
        {
            var parameter = new int[2];
            var cursor = new int[1];
            for (int i = 0; i < table[3]; i++)
            {
                int coded = GenericParameters.ReadDeferred(image, table, i, parameter, nominal: method);
                if (coded == 0) continue;
                if (parameter[1] != 0 && ConstraintMarkers.IsValueType(image, layout, heaps, coded)) continue;
                int end = 0;
                if ((coded & 3) == 2)
                {
                    end = Signature(image, layout, heaps, coded >> 2, cursor);
                    if (end < 0) return end;
                }
                if (end != 0 && image[cursor[0]] == 0x1f) // modreq
                {
                    int marker = CompressedUnsigned(image, cursor[0] + 1, end, cursor);
                    if (marker < 1 || cursor[0] >= end) return -1;
                    if (parameter[1] == 0 || image[cursor[0]] != 0x12 ||
                        !ConstraintMarkers.IsUnmanaged(image, layout, heaps, marker)) return -2;
                    int value = CompressedUnsigned(image, cursor[0] + 1, end, cursor);
                    if (value < 1 || cursor[0] != end) return -1;
                    if (!ConstraintMarkers.IsValueType(image, layout, heaps, value)) return -2;
                    continue;
                }

                int[] required = ReadInterface(bundle, image, layout, heaps, owner, objects, coded,
                    method ? null : arguments, context, method ? arguments : null);
                if (required[0] < 0) return required[0];
                int valid = ValidateInterface(bundle, objects, required, 0);
                if (valid <= 0) return valid < 0 ? valid : -2;
                int implemented = Implements(bundle, objects, arguments[parameter[0]], required, 0);
                if (implemented <= 0) return implemented < 0 ? implemented : -2;
            }
            return 0;
        }

        internal static int Implements(BundleImages bundle, int[] objects, int argument, int[] required, int depth)
        {
            var noParameters = new int[0];
            if (GenericScalar.IsScalar(argument))
            {
                // The primitive keeps its Wasm scalar storage. Read the
                // actual interface edges from the selected CoreLib instead.
                var definition = new int[2];
                int status = FindPrimitive(bundle, argument, definition);
                return status <= 0 ? status :
                    Search(bundle, objects, required, definition[0], definition[1], noParameters, 0, depth);
            }
            if (argument < ObjectLayouts.FirstClass || argument - ObjectLayouts.FirstClass >= objects[0]) return 0;
            for (int source = argument; source != 0; source = ObjectLayouts.Parent(objects, source))
            {
                int row = ObjectLayouts.TypeDef(objects, source);
                if (row <= 0) break;
                int status = Search(bundle, objects, required, ObjectLayouts.AssemblyOf(objects, source),
                    row, noParameters, source, depth);
                if (status != 0) return status;
            }
            return 0;
        }

        internal static int FindPrimitive(BundleImages bundle, int argument, int[] definition)
        {
            int definitionOwner = -1, definitionRow = 0;
            for (int owner = 0; owner < bundle.Count; owner++)
            {
                byte[] image = bundle.Get(owner);
                var layout = new int[4];
                var heaps = new int[8];
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                int row = ArrayTypeRefs.CoreDefinition(image, heaps, layout, GenericScalar.Element(argument));
                if (row < 0) return row;
                if (row == 0) continue;
                if (definitionOwner >= 0) return -5;
                definitionOwner = owner;
                definitionRow = row;
            }
            if (definitionOwner < 0) return 0;
            definition[0] = definitionOwner;
            definition[1] = definitionRow;
            return 1;
        }

        private static int Search(BundleImages bundle, int[] objects, int[] required,
            int owner, int row, int[] substitutions, int context, int depth)
        {
            if (depth >= ObjectLayouts.MaxTypeDepth) return -2;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var table = new int[3];
            int count = InterfaceImplementations.ReadTable(image, heaps, layout, table);
            if (count < 0) return count;

            // Visit direct edges before following inherited interfaces. An
            // unrelated unavailable edge cannot prove a nominal constraint.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < count; i++)
                {
                    int coded = InterfaceImplementations.Read(image, table, i, row);
                    if (coded < 0) return coded;
                    if (coded == 0) continue;
                    int[] edge = ReadInterface(bundle, image, layout, heaps, owner, objects, coded, substitutions, context);
                    if (edge[0] == -1) return -1;
                    if (edge[0] < 0) continue;
                    if (Same(edge, required)) return 1;
                    if (pass == 0) continue;
                    int valid = ValidateInterface(bundle, objects, edge, depth + 1);
                    if (valid == -1) return -1;
                    if (valid <= 0) continue;
                    var args = new int[edge.Length - 2];
                    for (int a = 0; a < args.Length; a++) args[a] = edge[2 + a];
                    int status = Search(bundle, objects, required, edge[0], edge[1], args, 0, depth + 1);
                    if (status != 0) return status;
                }
            return 0;
        }

        private static int ValidateInterface(BundleImages bundle, int[] objects, int[] identity, int depth)
        {
            if (depth >= ObjectLayouts.MaxTypeDepth) return -2;
            if (identity[0] < 0 || identity[1] < 1) return 0;
            byte[] image = bundle.Get(identity[0]);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var shape = new int[5];
            int status = ObjectLayouts.ReadShape(image, heaps, layout, identity[1], shape);
            if (status < 0) return status;
            if ((shape[4] & 0x20) == 0) return 0;

            var args = new int[identity.Length - 2];
            for (int i = 0; i < args.Length; i++) args[i] = identity[i + 2];
            var deferred = new int[8];
            status = GenericParameters.Check(image, heaps, layout, identity[1], args, objects, deferred);
            if (status <= 0) return status == 0 ? 1 : status;
            // A recursive bound such as INumberBase<TSelf> requires the
            // argument to implement this exact closed interface. Prove that
            // metadata edge directly instead of expanding the bound again.
            var parameter = new int[2];
            for (int i = 0; i < deferred[3]; i++)
            {
                int coded = GenericParameters.ReadDeferred(image, deferred, i, parameter);
                if (coded == 0) continue;
                int[] required = ReadInterface(bundle, image, layout, heaps, identity[0], objects, coded, args, 0);
                if (required[0] < 0) return required[0];
                if (!Same(required, identity)) return 0;
                int proof = Implements(bundle, objects, args[parameter[0]], identity, depth + 1);
                if (proof <= 0) return proof;
            }
            return 1;
        }

        internal static int[] ReadInterface(BundleImages bundle, byte[] image, int[] layout,
            int[] heaps, int owner, int[] objects, int coded, int[] substitutions, int context, int[] methodArguments = null)
        {
            var symbol = new int[2];
            if ((coded & 3) != 2)
            {
                int identity = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, coded, symbol);
                return identity < 0 ? Failure(identity) : symbol;
            }

            var cursor = new int[1];
            int end = Signature(image, layout, heaps, coded >> 2, cursor);
            if (end < 0) return Failure(end);
            int start = cursor[0];
            if (end - start < 4 || image[start] != 0x15 || image[start + 1] != 0x12) return Failure(-2);
            int named = CompressedUnsigned(image, start + 2, end, cursor);
            if (named < 1) return Failure(-1);
            int arity = CompressedUnsigned(image, cursor[0], end, cursor);
            if (arity < 1 || arity > end - cursor[0]) return Failure(-1);
            int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, named, symbol);
            if (status < 0) return Failure(status);
            var result = new int[arity + 2];
            result[0] = symbol[0];
            result[1] = symbol[1];
            for (int i = 0; i < arity; i++)
            {
                // This phase verifies the nominal edge without recursively
                // registering another layout in the middle of its constraints.
                int item = Argument(bundle, image, layout, heaps, owner, objects, cursor, end, substitutions, context, methodArguments);
                if (item < 0) return Failure(item);
                result[i + 2] = item;
            }
            return cursor[0] == end ? result : Failure(-1);
        }

        private static int Argument(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int[] objects, int[] cursor, int end, int[] substitutions, int context, int[] methodArguments)
        {
            int at = cursor[0];
            if (at >= end) return -1;
            int element = image[at];
            cursor[0] = at + 1;
            int scalar = GenericScalar.Key(element);
            if (scalar != 0) return scalar;
            if (element == 0x1c) return ObjectLayouts.ObjectReference;
            if (element == 0x0e) return SignatureTypes.String;
            if (element != 0x11 && element != 0x12 && element != 0x13 && element != 0x1e) return -2;
            int item = CompressedUnsigned(image, at + 1, end, cursor);
            if (item < 0) return item;
            if (element == 0x1e)
                return methodArguments != null && item < methodArguments.Length ? methodArguments[item] : -2;
            if (element == 0x13)
                return context != 0 ? ObjectLayouts.Argument(objects, context, item) :
                    substitutions != null && item < substitutions.Length ? substitutions[item] : -2;
            if (item == 0) return -1;
            if (element == 0x11)
            {
                int primitive = ArrayTypeRefs.PrimitiveElement(image, heaps, layout, item);
                if (primitive > 0) return GenericScalar.Key(primitive);
                if (primitive != -2) return primitive;
            }
            var symbol = new int[2];
            int status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, item, symbol);
            if (status < 0) return status;
            if (symbol[0] == AssemblyBundle.ObjectOwner) return element == 0x12 ? ObjectLayouts.ObjectReference : -2;
            int known = ObjectLayouts.Find(objects, symbol[0], symbol[1], new int[0]);
            return known != 0 && ObjectLayouts.IsValueType(objects, known) == (element == 0x11) ? known : -2;
        }

        private static int Signature(byte[] image, int[] layout, int[] heaps, int row, int[] cursor)
        {
            int blob = AssemblyReferences.TypeSpecBlob(image, heaps, layout, row);
            if (blob < 0) return blob;
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, cursor);
            return size > 0 && size <= limit - cursor[0] ? cursor[0] + size : -1;
        }

        internal static bool Same(int[] left, int[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i]) return false;
            return true;
        }

        private static int[] Failure(int status)
        {
            var result = new int[1];
            result[0] = status;
            return result;
        }
    }
}
