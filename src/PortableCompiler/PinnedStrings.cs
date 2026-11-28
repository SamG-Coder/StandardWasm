using PeLibrary;

namespace PortableCompiler
{
    // Locals signature elements of the two C# fixed-string forms (ECMA-335 II.23.2.9).
    internal static class PinnedLocals
    {
        internal const int Pinned = 0x45;
        // pinned string: the netstandard form, ldloc; conv.u; ... + OffsetToStringData.
        internal const int Text = 1;
        // pinned char&: the form that calls string.GetPinnableReference().
        internal const int Character = 2, Byte = 3, StaticPointer = 4;
        // pinned T[] over a primitive element (decision 0105, PinnedArrays).
        internal const int Array = 5;

        internal static int Kind(byte[] image, int cursor, int end)
        {
            if (image[cursor] == 0x0e) return Text;
            if (end - cursor >= 2 && image[cursor] == 0x10 && image[cursor + 1] == 0x03) return Character;
            if (end - cursor >= 2 && image[cursor] == 0x10 && image[cursor + 1] == 5) return Byte;
            return 0;
        }
    }

    // Stage 149 (decision 0077): fixed (char* p = s) pins nothing in Wasm GC. Its character
    // vector cannot be addressed by memory 0, so the address a fixed statement takes becomes
    // a frame-owned UTF-16 copy plus a terminating NUL on the shadow stack (decision 0065).
    // The copy lives until the frame returns, past the fixed statement's end. C# makes
    // writes through the pointer undefined (ECMA-334 23.7), so the copy is not written back.
    internal static class PinnedStrings
    {
        // Rewrites, before linking, the two compiler-generated pinning sequences:
        //   ldloc <pinned string>; conv.u        -> ldloc; PinString           [String] -> [Native]
        //   call RuntimeHelpers::get_OffsetToStringData -> ldc.i4.0 (the copy has no header)
        //   call string::GetPinnableReference(); stloc <pinned char&>
        //                                        -> PinString (null raises NullReferenceException)
        // Any other use of a pinned local keeps its ordinary meaning: a pinned string is
        // a String reference, a pinned char& a native address.
        internal static int Lower(byte[] image, int[] heaps, int[] layout, int[] records, int[] pinned,
            int arguments, int locals, int[] location)
        {
            bool text = false, character = false;
            for (int i = 0; i < locals; i++)
            {
                if (pinned[i] == PinnedLocals.Text) text = true;
                if (pinned[i] == PinnedLocals.Character) character = true;
            }
            if (!text && !character) return 0;
            int length = records.Length / BranchIl.Width, previous = -1;
            for (int pc = 0; pc < length;)
            {
                int slot = pc * BranchIl.Width, kind = records[slot], next = records[slot + 2];
                if (next <= pc) return -1;
                if (character && IsLocal(records, slot, BranchIl.Store, pinned, arguments, locals, PinnedLocals.Character))
                    records[slot + 3] = PinnedLocals.Character;
                if (text && kind == BranchIl.ConvertInteger && records[slot + 1] == 0xe0 && previous >= 0 &&
                    IsLocal(records, previous, BranchIl.Load, pinned, arguments, locals, PinnedLocals.Text))
                {
                    records[slot] = BranchIl.PinString;
                    records[slot + 1] = 0;
                    records[slot + 3] = 0; // Roslyn branches around the null string's zero address.
                }
                else if (kind == BranchIl.MemberCall && records[slot + 3] == 0x28)
                {
                    int member = records[slot + 1];
                    if (text && StringMetadata.IsOffsetToStringData(image, heaps, layout, member))
                    {
                        records[slot] = BranchIl.Constant;
                        records[slot + 1] = 0;
                        records[slot + 3] = 0;
                    }
                    else if (character && next < length &&
                        IsLocal(records, next * BranchIl.Width, BranchIl.Store, pinned, arguments, locals, PinnedLocals.Character) &&
                        StringMetadata.Member(image, heaps, layout, member) == StringMetadata.PinnableReference)
                    {
                        records[slot] = BranchIl.PinString;
                        records[slot + 1] = 0;
                        records[slot + 3] = 1; // the CLR dereferences a null receiver
                    }
                }
                else if (character && kind == BranchIl.Call && records[slot + 3] == 0 && next < length &&
                    IsLocal(records, next * BranchIl.Width, BranchIl.Store, pinned, arguments, locals, PinnedLocals.Character) &&
                    ResourceNativeMetadata.IsTrustedUtf16PinnableReference(image, heaps, layout, records[slot + 1]))
                {
                    // The static CoreLib marshaller passes null as a null char pointer.
                    records[slot] = BranchIl.PinString;
                    records[slot + 1] = 0;
                    records[slot + 3] = 0;
                }
                previous = slot;
                pc = next;
            }
            return 0;
        }

        private static bool IsLocal(int[] records, int slot, int kind, int[] pinned, int arguments, int locals, int expected)
        {
            int local = records[slot + 1] - arguments;
            return records[slot] == kind && local >= 0 && local < locals && pinned[local] == expected;
        }
    }
}
