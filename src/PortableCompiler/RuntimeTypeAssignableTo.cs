using PeLibrary;
using static PeLibrary.ObjectLayouts;
using static PortableCompiler.BranchIl;
using static PortableCompiler.BranchWasm;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal sealed partial class RuntimeTypeRelations
    {
        private const int NullDestination = -1, SameSource = -1, NullSource = -2, UnknownSource = -3;
        private const int Factory = 0, Source = 1, Function = 2, AssignableToWidth = 3;
        private int[] assignableTo = new int[4 * AssignableToWidth];
        private int assignableToCount;

        internal bool HasAssignableTo => assignableToCount != 0;

        internal int BindTo(BundleImages bundle, int[] objects, int[] globals, RuntimeGenericTypes factories,
            int[] records, int slot, int[] clauses, int[] owners, int[] arities, int[] offsets, int[] signatures,
            int used, int[] helper, bool callvirt)
        {
            int destination = Previous(records, slot, clauses);
            if (destination < 0) return used;
            int factory = NullDestination;
            int source;
            if (records[destination] == NullLiteral)
            {
                source = LiteralSource(records, Previous(records, destination, clauses), clauses);
            }
            else
            {
                if (records[destination] != Call || records[destination + 3] != 0) return used;
                var operands = new int[2];
                if (RuntimeGenericTypes.OpenDefinition(bundle, objects, records, destination, clauses, null, operands) < 0)
                    return used;
                int receiver = Previous(records, operands[0], clauses);
                int argument = operands[1];
                // Both appearances read the same frame slot with no intervening
                // writes. This proves which factory argument supplies the receiver.
                source = receiver >= 0 && records[receiver] == Load && records[argument] == Load &&
                    records[receiver + 1] == records[argument + 1] ? SameSource :
                    LiteralSource(records, receiver, clauses);
                factory = records[destination + 1];
            }
            // An explicit nonvirtual call has no implicit null check. Only
            // narrow that form when its literal receiver is known nonnull.
            if (source == UnknownSource || source < 0 && !callvirt) return used;
            if (factory != NullDestination)
            {
                int status = factories.RequireInvariantRelation(bundle, objects, factory);
                if (status <= 0) return status == 0 ? used : status;
                if (source >= 0)
                {
                    int literal = RuntimeGenericTypes.Argument(bundle, objects, globals, source,
                        RuntimeGenericTypes.CoreTypes(bundle), new int[0], prepareLiteral: true);
                    if (literal == -1 || literal == -3) return literal;
                }
            }

            for (int entry = 0; entry < assignableToCount * AssignableToWidth; entry += AssignableToWidth)
            {
                if (assignableTo[entry + Factory] != factory || assignableTo[entry + Source] != source) continue;
                helper[0] = assignableTo[entry + Function];
                return used;
            }

            int offset = MethodCompiler.NextSignature(offsets, arities, used);
            if (used >= owners.Length || signatures.Length - offset < 3) return -3;
            int at = assignableToCount++ * AssignableToWidth;
            if (at == assignableTo.Length)
            {
                var grown = new int[at * 2];
                for (int i = 0; i < at; i++) grown[i] = assignableTo[i];
                assignableTo = grown;
            }
            assignableTo[at + Factory] = factory;
            assignableTo[at + Source] = source;
            assignableTo[at + Function] = used;
            arities[used] = 2;
            offsets[used] = offset;
            signatures[offset] = I32;
            signatures[offset + 1] = RuntimeTypeMetadata.Type;
            signatures[offset + 2] = RuntimeTypeMetadata.Type;
            helper[0] = used;
            return used + 1;
        }

        private static int LiteralSource(int[] records, int slot, int[] clauses)
        {
            if (slot < 0) return UnknownSource;
            if (records[slot] == NullLiteral) return NullSource;
            if (records[slot] != TypeFromHandle) return UnknownSource;
            int token = Previous(records, slot, clauses);
            return token >= 0 && records[token] == LoadTypeToken ? records[token + 1] : UnknownSource;
        }

        internal int WriteAssignableTo(BundleImages bundle, byte[] code, int used, int[] objects, int[] globals,
            RuntimeGenericTypes factories, int[] offsets, int[] sizes)
        {
            if (assignableToCount == 0) return used;
            var core = RuntimeGenericTypes.CoreTypes(bundle);
            var noArguments = new int[0];
            int[] outcomes = factories.Relations;
            for (int entry = 0; entry < assignableToCount * AssignableToWidth; entry += AssignableToWidth)
            {
                int function = assignableTo[entry + Function];
                int factory = assignableTo[entry + Factory];
                int source = assignableTo[entry + Source];
                offsets[function] = used;
                if (code.Length - used < 28) return -3;
                code[used++] = 0; // no local declarations
                // callvirt tests its receiver even when the supplied target is null.
                used = GuardNull(code, used, 0);
                used = Local(code, used, 0x20, 1);
                code[used++] = 0xd1; // ref.is_null
                used = ReturnCase(code, used, 0);
                int literal = source < 0 ? 0 : RuntimeGenericTypes.Argument(bundle, objects, globals, source,
                    core, noArguments);
                for (int row = 0; row < factories.RelationCount * RuntimeGenericTypes.RelationWidth; row += RuntimeGenericTypes.RelationWidth)
                {
                    if (factory != outcomes[row + RuntimeGenericTypes.FactoryFunction] || source == NullSource) continue;
                    int sourceIdentity = source == SameSource ? outcomes[row + RuntimeGenericTypes.ArgumentType] : literal;
                    if (sourceIdentity <= 0) continue;
                    int target = outcomes[row + RuntimeGenericTypes.ResultType];
                    int answer = CompareFactoryInterface(bundle, objects, sourceIdentity, target);
                    if (answer < 0) continue;
                    int sourceGlobal = source == SameSource ? outcomes[row + RuntimeGenericTypes.ArgumentGlobal] : source;
                    if (code.Length - used < 32) return -3;
                    used = MatchGlobal(code, used, 0, sourceGlobal);
                    used = MatchGlobal(code, used, 1, outcomes[row + RuntimeGenericTypes.ResultGlobal]);
                    code[used++] = 0x71; // i32.and
                    used = ReturnCase(code, used, answer);
                }
                // Unrepresented or incompletely proven relations remain explicit.
                code[used++] = 0x00;
                code[used++] = 0x0b;
                sizes[function] = used - offsets[function];
            }
            return used;
        }

        private static int CompareFactoryInterface(BundleImages bundle, int[] objects, int source, int target)
        {
            if (source == ObjectReference) return 0;
            if (source == String || GenericScalar.IsScalar(source) || IsEnumKey(source) || IsValueType(objects, source))
                return BundleConstraints.CompareInvariantInterfaceMetadata(bundle, objects, source, target);
            if (source < FirstClass || source - FirstClass >= objects[0] || ArrayElementKey(objects, source) != 0) return -2;
            return Compare(bundle, objects, source, target);
        }

        private static int MatchGlobal(byte[] code, int used, int operand, int descriptor)
        {
            used = Local(code, used, 0x20, operand);
            code[used++] = 0x23; // global.get
            used = WasmBinary.Unsigned(code, used, descriptor);
            code[used++] = 0xd3; // ref.eq
            return used;
        }
    }
}
