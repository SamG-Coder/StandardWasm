using PeLibrary;
using static PeLibrary.ImageBytes;
using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class CurrentThreadBinding
    {
        internal static int Lower(byte[] image, int[] heaps, int[] layout, int owner,
            int caller, int[] records, int[] clauses, int[] frame, int parameters, int locals, int maximum,
            int[] objects, int[] globals)
        {
            // This private CoreLib method is the only admitted producer and consumer of the
            // handle. The managed slot remains live in this frame for the synchronous QCall.
            if (parameters != 0 || locals != 1 || maximum < 2 || clauses.Length != 0 ||
                caller < 1 || MemberFlags(image, layout, caller) != 0x10 ||
                !Named(image, heaps, layout, caller, "InitializeCurrentThread")) return 0;
            var counts = new int[45];
            if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0 ||
                !PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts)) return 0;

            int thread = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System.Threading", "Thread");
            int handle = ObjectLayouts.FindTypeDefinition(image, heaps, layout, "System.Runtime.CompilerServices", "ObjectHandleOnStack");
            if (thread < 1 || handle < 1 || ObjectLayouts.Owner(image, heaps, layout, caller, false) != thread) return 0;
            int type = frame[0];
            if (type < ObjectLayouts.FirstClass || type - ObjectLayouts.FirstClass >= objects[0] ||
                ObjectLayouts.IsValueType(objects, type) || ObjectLayouts.TypeDef(objects, type) != thread ||
                ObjectLayouts.AssemblyOf(objects, type) != owner) return 0;

            for (int slot = 0; slot < records.Length; slot += Width)
            {
                if (records[slot] != FrameAddress || records[slot + 1] != 0) continue;
                int create = records[slot + 2] * Width;
                if (create >= records.Length || records[create] != MethodSpecCall || records[create + 3] != 0) continue;
                int qcall = records[create + 2] * Width;
                if (qcall >= records.Length || records[qcall] != Call || records[qcall + 3] != 0 ||
                    Entry(records, create) || Entry(records, qcall)) continue;
                if (!IsHandleCreation(image, heaps, layout, counts, records[create + 1], thread, handle) ||
                    !IsThreadQCall(image, heaps, layout, counts, records[qcall + 1], thread, handle)) continue;

                var fields = new int[3];
                int status = BindFields(image, heaps, layout, objects, type, fields);
                if (status < 0) return status;
                int root = StaticFields.Register(globals, owner, CurrentThreadWasm.RootKey, type);
                if (root < 0) return root;
                int vector = ObjectLayouts.RegisterArray(objects, ObjectLayouts.ObjectReference);
                if (vector < 0) return vector;

                // Keep ldloca on the ordinary path so all existing aliases use the same cell.
                records[create] = Nop;
                records[qcall] = SetCurrentThreadHandle;
                records[qcall + 1] = vector;
                records[qcall + 3] = LocationFromIdentity(objects, type);
                // The original call token bytes are not instruction entries.
                records[qcall + Width + 1] = root;
                records[qcall + Width + 2] = fields[0];
                records[qcall + Width + 3] = fields[1];
                records[qcall + 2 * Width + 1] = fields[2];
            }
            return 0;
        }

        private static bool IsHandleCreation(byte[] image, int[] heaps, int[] layout, int[] counts, int row,
            int thread, int handle)
        {
            var specification = new int[MethodSpecifications.Width];
            if (MethodSpecifications.Read(image, heaps, layout, counts, row, specification) < 0 ||
                specification[MethodSpecifications.Arity] != 1) return false;
            int target = specification[MethodSpecifications.Target];
            if ((target & 1) != 0) return false;
            int create = target >> 1;
            int argument = specification[MethodSpecifications.Arguments], end = specification[MethodSpecifications.End];
            var next = new int[1];
            if (argument >= end || image[argument] != 0x12 ||
                CompressedUnsigned(image, argument + 1, end, next) != (thread << 2) || next[0] != end ||
                MemberFlags(image, layout, create) != 0x10 ||
                ObjectLayouts.Owner(image, heaps, layout, create, false) != handle ||
                !Named(image, heaps, layout, create, "Create")) return false;

            // Generic static 1, one argument, ObjectHandleOnStack (ref !!0).
            int start = Signature(image, heaps, layout, create, next);
            end = next[0];
            if (start < 0 || end - start < 8 || image[start] != 0x10 || image[start + 1] != 1 ||
                image[start + 2] != 1 || image[start + 3] != 0x11 ||
                CompressedUnsigned(image, start + 4, end, next) != (handle << 2)) return false;
            int rest = next[0];
            return end - rest == 3 && image[rest] == 0x10 && image[rest + 1] == 0x1e && image[rest + 2] == 0;
        }

        private static bool IsThreadQCall(byte[] image, int[] heaps, int[] layout, int[] counts, int row,
            int thread, int handle)
        {
            if (row < 1 || row > counts[6] || MemberFlags(image, layout, row) != 0x2010 ||
                MethodDefinitions.ReadColumn(image, layout, row, 1) != 0x80 ||
                ObjectLayouts.Owner(image, heaps, layout, row, false) != thread ||
                !Named(image, heaps, layout, row, "GetCurrentThread") ||
                NativeImports.Find(image, heaps, layout, counts, "QCall", "ThreadNative_GetCurrentThread", row, 0x101) != row)
                return false;
            var next = new int[1];
            int start = Signature(image, heaps, layout, row, next), end = next[0];
            // static void (ObjectHandleOnStack).
            return start >= 0 && end - start >= 5 && image[start] == 0 && image[start + 1] == 1 &&
                image[start + 2] == 1 && image[start + 3] == 0x11 &&
                CompressedUnsigned(image, start + 4, end, next) == (handle << 2) && next[0] == end;
        }

        private static int Signature(byte[] image, int[] heaps, int[] layout, int row, int[] end)
        {
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob < 1 || blob >= heaps[1]) return -1;
            int limit = heaps[0] + heaps[1];
            int size = CompressedUnsigned(image, heaps[0] + blob, limit, end);
            int start = end[0];
            if (size < 1 || size > limit - start) return -1;
            end[0] += size;
            return start;
        }

        private static int MemberFlags(byte[] image, int[] layout, int row) =>
            MethodDefinitions.ReadColumn(image, layout, row, 2) & 0x2010;

        private static bool Named(byte[] image, int[] heaps, int[] layout, int row, string name) =>
            MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, row, 3), name);

        private static bool Entry(int[] records, int slot) =>
            ValueOperands.Entry(records, slot / Width, new int[0]);

        private static int BindFields(byte[] image, int[] heaps, int[] layout, int[] objects, int type, int[] fields)
        {
            int table = ObjectLayouts.TableOf(objects, type), first = ObjectLayouts.FirstRow(objects, type);
            int count = ObjectLayouts.DeclaredFieldCount(objects, type);
            var definition = new int[3];
            string[] names = { "_managedThreadId", "_priority", "_DONT_USE_InternalThread" };
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = -1;
                for (int row = first; row < first + count; row++)
                {
                    AssemblyReferences.ReadField(image, layout, table, row, definition);
                    if (!MatchesAscii(image, heaps, (uint)definition[1], names[i])) continue;
                    if (fields[i] >= 0 || ObjectLayouts.FieldType(objects, type, row) != (i == 2 ? Native : I32)) return -2;
                    fields[i] = ObjectLayouts.FieldOrdinal(objects, type, row);
                }
                if (fields[i] < 0) return -2;
            }
            return 0;
        }
    }
}
