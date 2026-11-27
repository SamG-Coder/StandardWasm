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
                int coded = GenericParameters.ReadDeferred(image, table, i, parameter);
                if (coded == 0) continue;
                int marker = ValueConstraintMarker(image, layout, heaps, coded, parameter[1], cursor);
                if (marker < 0) return marker;
                if (marker != 0) continue;

                int argument = arguments[parameter[0]];
                int dependent = CheckTypeParameterConstraint(bundle, image, layout, heaps, owner, objects, coded,
                    argument, method ? null : arguments, context, method ? arguments : null, cursor, 0);
                if (dependent < 0) return dependent;
                if (dependent != 0) continue;

                int[] required = ReadInterface(bundle, image, layout, heaps, owner, objects, coded,
                    method ? null : arguments, context, method ? arguments : null);
                if (required[0] < 0) return required[0];
                int valid = ValidateInterface(bundle, objects, required, 0);
                if (valid <= 0) return valid < 0 ? valid : -2;
                int implemented = Implements(bundle, objects, argument, required, 0);
                if (implemented <= 0) return implemented < 0 ? implemented : -2;
            }
            return 0;
        }

        private static int CheckTypeParameterConstraint(BundleImages bundle, byte[] image, int[] layout, int[] heaps,
            int owner, int[] objects, int coded, int argument, int[] substitutions, int context,
            int[] methodArguments, int[] cursor, int depth)
        {
            if ((coded & 3) != 2) return 0;
            int end = Signature(image, layout, heaps, coded >> 2, cursor);
            if (end < 0) return end;
            int element = image[cursor[0]];
            if (element != 0x13 && element != 0x1e) return 0; // VAR or MVAR
            int target = Argument(bundle, image, layout, heaps, owner, objects, cursor, end, substitutions, context, methodArguments);
            if (target < 0) return target;
            if (cursor[0] != end) return -1;
            // Compare the substituted nominal types before examining boxing.
            // Int32 and UInt32 therefore remain distinct despite sharing i32 storage.
            if (argument == target || SignatureTypes.Assignable(argument, target, objects)) return 1;
            bool value = argument == SimdValueStorage.Vector128Word || GenericScalar.IsScalar(argument) || ObjectLayouts.IsEnumKey(argument) ||
                ObjectLayouts.IsValueType(objects, argument);
            if (!value) return -2;
            if (target == ObjectLayouts.ObjectReference) return 1;
            if (!ObjectLayouts.IsInterface(objects, target)) return -2;
            int arity = ObjectLayouts.ArgumentCount(objects, target);
            var required = new int[2 + arity];
            required[0] = ObjectLayouts.AssemblyOf(objects, target);
            required[1] = ObjectLayouts.TypeDef(objects, target);
            for (int i = 0; i < arity; i++) required[2 + i] = ObjectLayouts.Argument(objects, target, i);
            int implemented = Implements(bundle, objects, argument, required, depth);
            return implemented == 0 ? -2 : implemented;
        }

        // GenericParameters already checked the non-nullable value-type flag.
        // Handle recognized markers before walking the nominal interface hierarchy.
        private static int ValueConstraintMarker(byte[] image, int[] layout, int[] heaps,
            int coded, int valueParameter, int[] cursor)
        {
            if ((coded & 3) != 2)
                return valueParameter != 0 && ConstraintMarkers.IsValueType(image, layout, heaps, coded) ? 1 : 0;
            int end = Signature(image, layout, heaps, coded >> 2, cursor);
            if (end < 0) return end;
            if (image[cursor[0]] != 0x1f) return 0; // modreq
            int marker = CompressedUnsigned(image, cursor[0] + 1, end, cursor);
            if (marker < 1 || cursor[0] >= end) return -1;
            if (valueParameter == 0 || image[cursor[0]] != 0x12 ||
                !ConstraintMarkers.IsUnmanaged(image, layout, heaps, marker)) return -2;
            int value = CompressedUnsigned(image, cursor[0] + 1, end, cursor);
            if (value < 1 || cursor[0] != end) return -1;
            return ConstraintMarkers.IsValueType(image, layout, heaps, value) ? 1 : -2;
        }

        internal static int Implements(BundleImages bundle, int[] objects, int argument, int[] required, int depth)
        {
            if (argument == SignatureTypes.String)
                return ImplementsCoreReference(bundle, objects, required, "String", "System", depth);
            if (argument == SimdValueStorage.Vector128Word)
            {
                if (!Vector128Layouts.ProvesWordStorage(bundle)) return -2;
                var vector = new int[2];
                int status = RuntimeTypeTokens.FindCoreType(bundle, "Vector128`1", vector, "System.Runtime.Intrinsics");
                return status < 0 ? status : Search(bundle, objects, required, vector[0], vector[1],
                    new[] { GenericScalar.Key(7) }, 0, depth);
            }
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
            // An interface satisfies its own constraint without an InterfaceImpl edge.
            // Match the arguments as well as the definition to preserve closed identity.
            if (ObjectLayouts.IsInterface(objects, argument) && MatchesLayout(objects, argument, required))
                return 1;
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

        private static bool MatchesLayout(int[] objects, int argument, int[] required)
        {
            int arity = ObjectLayouts.ArgumentCount(objects, argument);
            if (ObjectLayouts.AssemblyOf(objects, argument) != required[0] ||
                ObjectLayouts.TypeDef(objects, argument) != required[1] || arity != required.Length - 2)
                return false;
            for (int i = 0; i < arity; i++)
                if (ObjectLayouts.Argument(objects, argument, i) != required[i + 2])
                    return false;
            return true;
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

        // Runtime representations such as String do not have nominal layouts.
        // Walk the metadata hierarchy without reserving an unused class layout.
        internal static int ImplementsCoreReference(BundleImages bundle, int[] objects, int[] required,
            string name, string space, int depth = 0, bool requireComplete = false)
        {
            if (depth >= ObjectLayouts.MaxTypeDepth) return -2;
            var identity = new int[2];
            int status = RuntimeTypeTokens.FindCoreType(bundle, name, identity, space);
            if (status < 0) return status;
            var arguments = new int[0];
            var layout = new int[4];
            var heaps = new int[8];
            var shape = new int[5];
            for (int level = 0; level < ObjectLayouts.MaxTypeDepth; level++)
            {
                int owner = identity[0], row = identity[1];
                if (owner == AssemblyBundle.ObjectOwner) return 0;
                status = Search(bundle, objects, required, owner, row, arguments, 0, depth, requireComplete);
                if (status != 0) return status;
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                status = ObjectLayouts.ReadShape(image, heaps, layout, row, shape);
                if (status < 0) return status;
                status = AssemblyBundle.ClassIdentity(bundle, image, layout, heaps, owner, shape[3], identity);
                if (status < 0) return status;
            }
            return -2;
        }

        // Value layouts intentionally omit interface edges and their ValueType/Enum
        // bases. Use the original metadata for reflection rather than the Wasm layout.
        internal static int CompareInvariantInterfaceMetadata(BundleImages bundle, int[] objects, int source, int target)
        {
            var required = new[] { ObjectLayouts.AssemblyOf(objects, target), ObjectLayouts.TypeDef(objects, target),
                ObjectLayouts.Argument(objects, target, 0) };
            if (source == SignatureTypes.String)
                return ImplementsCoreReference(bundle, objects, required, "String", "System", requireComplete: true);
            var definition = new int[2];
            bool enumeration = ObjectLayouts.IsEnumKey(source);
            int context = 0;
            if (enumeration) ObjectLayouts.EnumIdentity(objects, source, definition);
            else if (GenericScalar.IsScalar(source))
            {
                int found = FindPrimitive(bundle, source, definition);
                if (found <= 0) return found < 0 ? found : -2;
            }
            else
            {
                if (!ObjectLayouts.IsValueType(objects, source) || ObjectLayouts.IsNullableValueType(objects, source)) return -2;
                definition[0] = ObjectLayouts.AssemblyOf(objects, source);
                definition[1] = ObjectLayouts.TypeDef(objects, source);
                context = source;
            }
            int result = Search(bundle, objects, required, definition[0], definition[1],
                new int[0], context, 0, requireComplete: true);
            if (result != 0) return result;
            if (enumeration)
            {
                result = RuntimeTypeTokens.FindCoreType(bundle, "Enum", definition);
                if (result < 0) return result;
                result = Search(bundle, objects, required, definition[0], definition[1],
                    new int[0], 0, 0, requireComplete: true);
                if (result != 0) return result;
            }
            return ImplementsCoreReference(bundle, objects, required, "ValueType", "System", requireComplete: true);
        }

        internal static int ImplementsCoreEnum(BundleImages bundle, int[] objects, int[] required)
        {
            var identity = new int[2];
            var arguments = new int[0];
            int status = RuntimeTypeTokens.FindCoreType(bundle, "Enum", identity);
            if (status < 0) return status;
            status = Search(bundle, objects, required, identity[0], identity[1], arguments, 0, 0);
            if (status != 0) return status;
            // Read the interfaces directly. System.Enum and its ValueType base
            // are CLR runtime types, not ordinary reference layout definitions.
            status = RuntimeTypeTokens.FindCoreType(bundle, "ValueType", identity);
            return status < 0 ? status : Search(bundle, objects, required, identity[0], identity[1], arguments, 0, 0);
        }

        private static int Search(BundleImages bundle, int[] objects, int[] required,
            int owner, int row, int[] substitutions, int context, int depth, bool requireComplete = false)
        {
            if (depth >= ObjectLayouts.MaxTypeDepth) return -2;
            byte[] image = bundle.Get(owner);
            var layout = new int[4];
            var heaps = new int[8];
            if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
            var table = new int[3];
            int count = InterfaceImplementations.ReadTable(image, heaps, layout, table);
            if (count < 0) return count;

            bool incomplete = false;
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
                    if (edge[0] < 0)
                    {
                        incomplete = true;
                        continue;
                    }
                    if (Same(edge, required)) return 1;
                    if (pass == 0) continue;
                    int valid = ValidateInterface(bundle, objects, edge, depth + 1);
                    if (valid == -1) return -1;
                    if (valid <= 0)
                    {
                        incomplete = true;
                        continue;
                    }
                    var args = new int[edge.Length - 2];
                    for (int a = 0; a < args.Length; a++) args[a] = edge[2 + a];
                    int status = Search(bundle, objects, required, edge[0], edge[1], args, 0, depth + 1, requireComplete);
                    if (status > 0 || status == -1 || !requireComplete && status < 0) return status;
                    if (status < 0) incomplete = true;
                }
            return requireComplete && incomplete ? -2 : 0;
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
            var cursor = new int[1];
            for (int i = 0; i < deferred[3]; i++)
            {
                int coded = GenericParameters.ReadDeferred(image, deferred, i, parameter);
                if (coded == 0) continue;
                int marker = ValueConstraintMarker(image, layout, heaps, coded, parameter[1], cursor);
                if (marker < 0) return marker;
                if (marker != 0) continue;
                int dependent = CheckTypeParameterConstraint(bundle, image, layout, heaps, identity[0], objects,
                    coded, args[parameter[0]], args, 0, null, cursor, depth + 1);
                if (dependent < 0) return dependent;
                if (dependent != 0) continue;
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
            if (element == 0x15)
            {
                // Vector128's interface edges name the vector itself, with
                // its declaring lane substituted in the closed constraint.
                cursor[0] = at;
                int coded = SignatureReader.ReadSingleArgumentValue(image, cursor, end);
                if (coded <= 0 || cursor[0] >= end || !VectorLaneCount.IsVector128(image, layout, heaps, coded)) return -2;
                int laneElement = image[cursor[0]];
                if (laneElement != 7 && laneElement != 0x11 && laneElement != 0x13 && laneElement != 0x1e)
                    return -2;
                int lane = Argument(bundle, image, layout, heaps, owner, objects, cursor, end,
                    substitutions, context, methodArguments);
                if (lane < 0) return lane;
                return lane == GenericScalar.Key(7) && Vector128Layouts.ProvesWordStorage(bundle)
                    ? SimdValueStorage.Vector128Word : -2;
            }
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
