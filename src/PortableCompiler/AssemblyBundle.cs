using PeLibrary;

namespace PortableCompiler
{
    internal static class AssemblyBundle
    {
        internal const int ObjectOwner = -1; // Canonical root has no emitted TypeDef slot.
        // Friend access has not been evaluated yet; distinct from every FriendAssemblies.Allows result.
        private const int FriendUnknown = int.MinValue;
        // symbol receives [owner, MethodDef, constructor declaring TypeDef or 0,
        // closed declaring layout or 0, dispatch flags, protected receiver,
        // optional non-constructor instance declaring TypeDef]. context is the caller's own closed
        // declaring layout, which substitutes VAR in a TypeSpec parent.
        internal static int Resolve(BundleImages bundle, byte[] caller,
            int[] layout, int[] heaps, int owner, int member, int opcode, bool baseCall, int[] objects, int[] symbol, int context, int callerType,
            bool allowVirtual = false, bool valueAddress = false,
            int methodArity = 0, int[] callerMethodArguments = null, int callerReceiver = 0)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            int status = AssemblyReferences.ReadMember(caller, heaps, layout, member, reference, allowClosedParent: true);
            if (status < 0) return status;
            var binding = new int[2];
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            var range = new int[2];
            byte[] target;
            int matched, declaring, closed = 0;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent != 0)
            {
                // The closed layout already proved the definition's identity,
                // type accessibility and argument constraints.
                closed = ManagedReferenceSignatures.ReadInstantiation(bundle, caller, layout, heaps,
                    owner, parent, objects, context, callerMethodArguments);
                if (closed < ObjectLayouts.FirstClass) return closed < 0 ? closed : -2;
                if (ObjectLayouts.IsInterface(objects, closed) && (opcode != 0x6f || !allowVirtual || methodArity != 0)) return -2;
                // Direct calls to closed value types can carry a managed
                // receiver. The linked signature proves its exact stack type.
                if (ObjectLayouts.IsValueType(objects, closed) && opcode != 0x73 && opcode != 0x28) return -2;
                matched = ObjectLayouts.AssemblyOf(objects, closed);
                declaring = ObjectLayouts.TypeDef(objects, closed);
                target = bundle.Get(matched);
                if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
                status = ObjectLayouts.MethodRange(target, targetHeaps, targetLayout, declaring, range);
                if (status < 0) return status;
            }
            else
            {
                target = ResolveType(bundle, caller, heaps, layout, reference, false,
                    binding, targetLayout, targetHeaps, range);
                matched = binding[0];
                declaring = binding[1];
                if (declaring < 0) return declaring;
            }
            int row = 0;
            int friend = FriendUnknown;
            for (int candidate = range[0]; candidate < range[1]; candidate++)
            {
                int flags = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 2);
                int access = flags & 7;
                // The cheap static and name filters run first, so the friend metadata is read
                // only for a candidate that could actually bind this call.
                bool isStatic = (flags & 0x10) != 0;
                if (isStatic && opcode != 0x28) continue;
                int name = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 3);
                if (!AssemblyReferences.SameString(caller, heaps, reference[4], target, targetHeaps, name))
                    continue;
                // A closed parent in the caller's own assembly may name assembly
                // members and private members of the caller or an enclosing type.
                bool local = closed != 0 && matched == owner &&
                    (access == 3 || access == 5 || access == 1 &&
                        HasPrivateAccess(target, targetHeaps, targetLayout, callerType, declaring));
                // Assembly (internal) and FamORAssem (protected internal) members of another
                // bundle assembly are accessible when it names the caller in InternalsVisibleTo,
                // with the identity rules of decision 0028 (decision 0055). Access through
                // inheritance is checked separately below.
                if (!local && matched >= 0 && matched != owner && (access == 3 || access == 5))
                {
                    if (friend == FriendUnknown)
                    {
                        friend = FriendAccess(target, targetHeaps, targetLayout, caller, heaps, layout);
                    }
                    if (friend < 0) return friend;
                    local = friend != 0;
                }
                bool familyAccess = !isStatic && (access == 4 || access == 5);
                bool family = false;
                if (access != 6 && !local)
                {
                    if (!familyAccess) continue;
                    // A virtual call from its own closed declaring class uses the
                    // same receiver constraint as the ordinary virtual call shape.
                    if (callerReceiver >= ObjectLayouts.FirstClass &&
                        ObjectLayouts.AssemblyOf(objects, callerReceiver) == owner &&
                        ObjectLayouts.TypeDef(objects, callerReceiver) == callerType)
                    {
                        if (opcode == 0x6f && (flags & 0x40) != 0)
                        {
                            family = allowVirtual && closed == callerReceiver &&
                                !ObjectLayouts.IsInterface(objects, closed);
                        }
                        else if (opcode == 0x28 || opcode == 0x6f)
                        {
                            int declaringLayout = closed;
                            if (declaringLayout == 0)
                                declaringLayout = ObjectLayouts.Find(objects, matched, declaring, new int[0]);
                            // The trusted System.Object has no nominal layout to walk.
                            family = ObjectLayouts.Inherits(objects, callerReceiver, declaringLayout) ||
                                declaringLayout == 0 && !ObjectLayouts.IsValueType(objects, callerReceiver) &&
                                ObjectLayouts.IsCoreObjectType(target, targetHeaps, targetLayout, declaring << 2);
                        }
                    }
                    if (!family && !(baseCall && opcode == 0x28)) continue;
                }
                int signature = MethodDefinitions.ReadColumn(target, targetLayout, candidate, 4);
                int match = methodArity == 0
                    ? BundleSignatures.Match(bundle, caller, layout, heaps, owner, reference[5],
                        target, targetLayout, targetHeaps, matched, signature, false, objects, closed)
                    : GenericMethodSignatures.Match(bundle, caller, layout, heaps, owner, reference[5],
                        target, targetLayout, targetHeaps, matched, signature, methodArity, !isStatic);
                if (match < 0) return match;
                if (match == 0) continue;
                // Bind the declaration. The caller may defer virtual slot
                // selection until it has proved the receiver's type.
                bool constructor = ObjectLayouts.IsConstructor(target, targetHeaps, targetLayout, candidate);
                if (opcode == 0x73)
                {
                    if (!constructor || (flags & 0x1800) != 0x1800) return -2;
                }
                // call .ctor runs a base constructor, or constructs a value on a
                // location's address. The linker then requires a value receiver.
                else if (constructor ? !baseCall && !valueAddress || opcode != 0x28 : access != 6 && !local && !family) return -2;
                if (opcode == 0x6f && (flags & 0x40) != 0 && !allowVirtual) return -2;
                if (row != 0) return -5;
                row = candidate;
                symbol[2] = constructor ? declaring : 0;
                if (symbol.Length > 6 && !isStatic && !constructor) symbol[6] = declaring;
                if (allowVirtual) symbol[4] = flags & 0x60; // Virtual and Final
                if (family && !constructor) symbol[5] = callerReceiver;
            }
            if (row == 0) return -6;
            symbol[0] = matched;
            symbol[1] = row;
            symbol[3] = closed;
            return 0;
        }

        internal static int ResolveField(BundleImages bundle, byte[] caller,
            int[] layout, int[] heaps, int owner, int member, bool write, bool staticField, int[] objects, int[] globals, int[] field, int context, int callerType, int[] methodArguments = null, bool constructorFieldWrite = false,
            bool callerInitializer = false)
        {
            var reference = new int[AssemblyReferences.SymbolWidth];
            int status = AssemblyReferences.ReadMember(caller, heaps, layout, member, reference, allowClosedParent: true);
            if (status < 0) return status;
            var binding = new int[2];
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            var range = new int[2];
            byte[] target;
            int matched, declaringType, type = 0;
            int parent = reference[AssemblyReferences.GenericParent];
            if (parent != 0)
            {
                type = ManagedReferenceSignatures.ReadInstantiation(bundle, caller, layout, heaps,
                    owner, parent, objects, context, methodArguments);
                if (type < ObjectLayouts.FirstClass) return type < 0 ? type : -2;
                matched = ObjectLayouts.AssemblyOf(objects, type);
                declaringType = ObjectLayouts.TypeDef(objects, type);
                target = bundle.Get(matched);
                if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) return -1;
                range[0] = ObjectLayouts.FirstRow(objects, type);
                range[1] = range[0] + ObjectLayouts.DeclaredFieldCount(objects, type);
            }
            else
            {
                target = ResolveType(bundle, caller, heaps, layout, reference, true,
                    binding, targetLayout, targetHeaps, range);
                matched = binding[0];
                declaringType = binding[1];
                if (declaringType < 0) return declaringType;
            }

            int table = AssemblyReferences.FieldTable(target, targetHeaps, targetLayout);
            if (table < 0) return table;
            var definition = new int[3];
            int row = 0, flags = 0;
            for (int candidate = range[0]; candidate < range[1]; candidate++)
            {
                AssemblyReferences.ReadField(target, targetLayout, table, candidate, definition);
                if (!AssemblyReferences.SameString(caller, heaps, reference[4], target, targetHeaps, definition[1]))
                    continue;
                int match = BundleSignatures.Match(bundle, caller, layout, heaps, owner, reference[5],
                    target, targetLayout, targetHeaps, matched, definition[2], true, objects, type);
                if (match < 0) return match;
                if (match == 0) continue;
                // Closed parents in the caller's assembly follow the same rule
                // as methods: assembly members and private members of the
                // caller or an enclosing type.
                flags = definition[0];
                int access = flags & 7;
                bool local = parent != 0 && matched == owner &&
                    (access == 3 || access == 5 || access == 1 &&
                        HasPrivateAccess(target, targetHeaps, targetLayout, callerType, declaringType));
                if (!local && matched >= 0 && matched != owner && (access == 3 || access == 5))
                {
                    int friend = FriendAccess(target, targetHeaps, targetLayout, caller, heaps, layout);
                    if (friend < 0) return friend;
                    local = friend != 0;
                }
                // A linked initonly field may be written in a constructor of its own closed declaring type.
                bool initializesReceiver = constructorFieldWrite && type == context &&
                    matched == owner && declaringType == callerType;
                bool initializesStatic = staticField && callerInitializer && type == context &&
                    matched == owner && declaringType == callerType;
                if (((flags & 0x10) != 0) != staticField || (access != 6 && !local) ||
                    (write && (flags & 0x20) != 0 && !initializesReceiver && !initializesStatic)) return -2;
                if (initializesStatic) write = false;
                if (row != 0) return -5;
                row = candidate;
            }
            if (row == 0) return -6;
            if (staticField)
                return StaticFields.Bind(bundle, target, targetLayout, targetHeaps, matched, declaringType,
                    row, table, flags, write, objects, globals, field, type);
            if (type == 0) type = ObjectLayouts.Find(objects, matched, declaringType, new int[0]);
            if (type == 0)
                type = BundleLayouts.Register(bundle, target, targetLayout, targetHeaps, matched, declaringType, objects);
            if (type < 0) return type;
            int index = ObjectLayouts.FieldOrdinal(objects, type, row);
            if (index < 0) return index;
            field[0] = index;
            // The receiver layout and value type come from the same owning DLL.
            field[1] = FieldAccess.Encode(type, ObjectLayouts.FieldType(objects, type, row));
            field[2] = row;
            field[3] = flags;
            return 0;
        }

        private static bool HasPrivateAccess(byte[] image, int[] heaps, int[] layout,
            int callerType, int declaringType)
        {
            if (callerType < 1 || declaringType < 1) return false;
            if (callerType == declaringType) return true;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                callerType > counts[2] || declaringType > counts[2]) return false;

            // TypeDef rows here share one module. The enclosing chain must finish
            // at a top-level type within the number of recorded nesting relations.
            bool found = false;
            for (int depth = 0; depth <= counts[41]; depth++)
            {
                callerType = NestedTypes.Enclosing(image, heaps, layout, counts, callerType);
                if (callerType <= 0) return callerType == 0 && found;
                if (callerType == declaringType) found = true;
            }
            return false;
        }

        private static int FriendAccess(byte[] target, int[] targetHeaps, int[] targetLayout,
            byte[] caller, int[] callerHeaps, int[] callerLayout)
        {
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(target, targetHeaps[4], targetHeaps[5], counts) < 0) return -1;
            return FriendAssemblies.Allows(target, targetHeaps, targetLayout, counts, caller, callerHeaps, callerLayout);
        }

        internal static int ClassIdentity(BundleImages bundle, byte[] image,
            int[] layout, int[] heaps, int owner, int coded, int[] symbol)
        {
            int row = coded >> 2;
            if (row < 1) return -1;
            if (ObjectLayouts.IsCoreObjectType(image, heaps, layout, coded))
            {
                symbol[0] = ObjectOwner;
                symbol[1] = 0;
                return 0;
            }
            if ((coded & 3) == 0)
            {
                symbol[0] = owner;
                symbol[1] = row;
                return 0;
            }
            if ((coded & 3) != 1 || bundle.Length == 0) return -2;
            var reference = new int[AssemblyReferences.SymbolWidth];
            int status = AssemblyReferences.ReadType(image, heaps, layout, row, reference);
            if (status < 0) return status;
            var targetLayout = new int[4];
            var targetHeaps = new int[8];
            byte[] target = ResolveType(bundle, image, heaps, layout, reference, false,
                symbol, targetLayout, targetHeaps, new int[2]);
            if (symbol[1] < 0) return symbol[1];
            if (ObjectLayouts.IsCoreObjectType(target, targetHeaps, targetLayout, symbol[1] << 2))
            {
                symbol[0] = ObjectOwner;
                symbol[1] = 0;
            }
            return 0;
        }

        // Find the final TypeDef. Keep the caller's type/member names and
        // signature fixed while each facade supplies a fresh AssemblyRef.
        // binding carries (owner, TypeDef) on success, or a negative status in slot 1.
        private static byte[] ResolveType(BundleImages bundle, byte[] caller,
            int[] heaps, int[] layout, int[] reference, bool fields, int[] binding, int[] targetLayout,
            int[] targetHeaps, int[] range)
        {
            int owner = FindOwner(bundle, caller, heaps, layout, reference);
            int status = owner;
            for (int hop = 0; owner >= 0 && hop < bundle.Count; hop++)
            {
                status = -1;
                byte[] target = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(target, targetLayout, targetHeaps) < 0) break;
                status = AssemblyReferences.FindType(caller, heaps, layout, reference,
                    target, targetHeaps, targetLayout, range, fields);
                if (status > 0)
                {
                    binding[0] = owner;
                    binding[1] = status;
                    return target;
                }
                if (status != -6) break;

                int forward = TypeForwarders.Find(caller, heaps, reference, target, targetHeaps, targetLayout);
                status = forward;
                if (status < 0) break;
                var next = new int[AssemblyReferences.SymbolWidth];
                status = AssemblyReferences.ReadAssembly(target, targetHeaps, targetLayout, forward, next);
                if (status < 0) break;
                owner = FindOwner(bundle, target, targetHeaps, targetLayout, next);
                // A fixed type name can visit each bundled owner only once.
                status = owner < 0 ? owner : -1;
            }
            binding[1] = status;
            return bundle.Get(0);
        }

        private static int FindOwner(BundleImages bundle, byte[] caller, int[] heaps, int[] layout, int[] reference)
        {
            int matched = -1;
            var candidateLayout = new int[4];
            var candidateHeaps = new int[8];
            for (int owner = 0; owner < bundle.Count; owner++)
            {
                byte[] candidate = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(candidate, candidateLayout, candidateHeaps) < 0)
                    return -1;
                int match = AssemblyReferences.Matches(caller, heaps, layout, reference, candidate, candidateHeaps, candidateLayout);
                if (match < 0) return match;
                if (match == 0) continue;
                if (matched >= 0) return -5;
                matched = owner;
            }
            if (matched < 0)
            {
                bundle.MissingImage = caller;
                bundle.MissingHeaps = heaps;
                var copy = new int[reference.Length];
                for (int i = 0; i < reference.Length; i++) copy[i] = reference[i];
                bundle.MissingReference = copy;
                return -4;
            }
            return matched;
        }
    }
}
