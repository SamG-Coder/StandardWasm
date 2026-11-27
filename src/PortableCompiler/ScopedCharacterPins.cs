using static PortableCompiler.BranchIl;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    // Keep a fixed byref as a traced alias until its synchronous native use.
    // Only a single direct call, with no way to take or store the pointer,
    // can use the temporary UTF-16 buffer. Unproven forms reach the existing
    // located diagnostic at the pinned-local store.
    internal static class ScopedCharacterPins
    {
        internal static bool Lower(int[] records, int[] frame, int[] pinned, int parameters, int locals, int[] clauses, int[] frameCells, NativeImportTable imports, int nativeBase)
        {
            if (clauses.Length != 0) return false;
            bool lowered = false;
            for (int i = 0; i < locals; i++)
            {
                if (pinned[i] != PinnedLocals.Character) continue;
                if (LowerLocal(records, frame, parameters, parameters + i, clauses, frameCells, imports, nativeBase)) lowered = true;
            }
            return lowered;
        }

        private static bool LowerLocal(int[] records, int[] frame, int parameters, int local, int[] clauses, int[] frameCells, NativeImportTable imports, int nativeBase)
        {
            if (frameCells[local] != 0) return false;
            int start = -1, load = -1, clear = -1, zero = -1, clearConvert = -1;
            int previous = -1, beforePrevious = -1;
            for (int slot = 0; slot < records.Length; slot = Next(records, slot))
            {
                int kind = records[slot];
                if (UsesLocal(kind) && records[slot + 1] == local)
                {
                    if (kind == Load && load < 0)
                        load = slot;
                    else if (kind == Store && start < 0 && previous >= 0 && records[previous] == Load &&
                        frame[records[previous + 1]] == CharacterLocation)
                        start = slot;
                    else if (kind == Store && clear < 0 && IsUnsignedConversion(records, previous) &&
                        beforePrevious >= 0 && records[beforePrevious] == Constant && records[beforePrevious + 1] == 0)
                    {
                        clear = slot;
                        zero = beforePrevious;
                        clearConvert = previous;
                    }
                    else return false;
                }
                beforePrevious = previous;
                previous = slot;
            }
            if (start < 0 || load <= start) return false;
            int conversion = Next(records, load);
            if (!IsUnsignedConversion(records, conversion)) return false;
            int use = Next(records, conversion), pointer = -1;
            if (use < records.Length && records[use] == Store)
            {
                pointer = records[use + 1];
                if (pointer < parameters || frame[pointer] != Native || frameCells[pointer] != 0) return false;
                int store = use;
                use = -1;
                for (int slot = 0; slot < records.Length; slot = Next(records, slot))
                {
                    if (!UsesLocal(records[slot]) || records[slot + 1] != pointer || slot == store) continue;
                    if (slot <= store || records[slot] != Load || use >= 0) return false;
                    use = slot;
                }
            }
            if (use < 0 || use >= records.Length) return false;
            int token = pointer < 0 ? use : Next(records, use);
            if (token >= records.Length || records[token] != Load || !IsNativeInteger(frame[records[token + 1]]))
                return false;
            int call = Next(records, token);
            if (call >= records.Length || records[call] != CallDynamicNative || clear >= 0 && clear <= call)
                return false;

            int last = clear >= 0 ? clear : call;
            for (int slot = start; slot <= last; slot = Next(records, slot))
            {
                int kind = records[slot];
                if (kind == Branch || kind == Conditional || kind == Switch || kind == Leave || kind == Return ||
                    ValueOperands.Entry(records, slot / Width, clauses)) return false;
            }

            frame[local] = CharacterLocation;
            records[start + 3] = 0;
            if (pointer >= 0) frame[pointer] = CharacterLocation;
            records[conversion] = Nop;
            records[call] = CallPinnedCharacterNative;
            records[call + 1] = nativeBase + NativeIndirectImports.RegisterScopedUtf16(imports);
            if (clear >= 0)
            {
                records[zero] = Nop;
                records[clear + 3] = 0;
                records[clearConvert] = NullManagedLocation;
                records[clearConvert + 3] = CharacterLocation;
            }
            return true;
        }

        private static int Next(int[] records, int slot)
        {
            int next = records[slot + 2] * Width;
            return next > slot ? next : slot + Width;
        }

        private static bool UsesLocal(int kind) =>
            kind == Load || kind == Store || kind == FrameAddress || kind == LoadFrameLocation;

        private static bool IsUnsignedConversion(int[] records, int slot) =>
            slot >= 0 && slot < records.Length && records[slot] == ConvertInteger && records[slot + 1] == 0xe0;
    }
}
