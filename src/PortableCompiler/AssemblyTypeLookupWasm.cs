using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class AssemblyTypeLookupWasm
    {
        private const int Input = 4, Catalog = 5, Start = 6, Length = 7, Module = 8;

        internal static int Write(byte[] code, int used, AssemblyTypeLookup lookup,
            RuntimeReflectionPlan reflection, int[] offsets, int[] sizes, int hostImports)
        {
            int function = lookup.Function;
            if (function < 0) return used;
            int remaining = code.Length - used;
            int modules = lookup.ModuleStarts.Length - 1;
            int count = lookup.ModuleStarts[modules] / AssemblyTypeLookup.EntryWidth;
            if (remaining < 640 || modules > (remaining - 640) / 16 ||
                count > (remaining - 640 - modules * 16) / 48) return -3;

            offsets[function] = used;
            code[used++] = 2; // two Char[] locals and three i32 locals
            code[used++] = 2;
            used = WasmBinary.ValueType(code, used, SignatureTypes.Chars);
            code[used++] = 3; code[used++] = 0x7f;
            used = GuardNull(code, used, 0);
            used = GuardNull(code, used, 1, ExceptionTagsWasm.ArgumentNullTag);
            used = Local(code, used, 0x20, 1);
            used = StringWasm.Unwrap(code, used);
            used = Local(code, used, 0x22, Input);
            code[used++] = 0xfb; code[used++] = 0x0f; // array.len
            used = Local(code, used, 0x22, Length);
            code[used++] = 0x45;
            used = ExceptionTagsWasm.ThrowIf(code, used, ExceptionTagsWasm.ArgumentTag);

            used = SkipLeadingSpace(code, used);
            used = RuntimeReflectionWasm.ModuleSlot(code, used, 0, 0, reflection);
            used = Local(code, used, 0x21, Module);
            used = StringWasm.LoadLiteral(code, used, lookup.Catalog, lookup.CatalogStart, lookup.CatalogLength);
            used = StringWasm.Unwrap(code, used);
            used = Local(code, used, 0x21, Catalog);
            used = Local(code, used, 0x20, Length);
            used = Local(code, used, 0x20, Start);
            code[used++] = 0x6b; // remaining input length
            used = Local(code, used, 0x21, Length);

            for (int owner = 0; owner < modules; owner++)
            {
                int begin = lookup.ModuleStarts[owner], end = lookup.ModuleStarts[owner + 1];
                if (begin == end && lookup.HasExports[owner] == 0) continue;
                used = Local(code, used, 0x20, Module);
                used = Const(code, used, owner + 1);
                code[used++] = 0x46;
                used = If(code, used);
                for (int entry = begin; entry < end; entry += AssemblyTypeLookup.EntryWidth)
                    used = CompareEntry(code, used, lookup.Entries, entry, hostImports + function + 1);
                // This profile cannot conclude absence when metadata names
                // can be supplied by a forwarded type or another module.
                if (lookup.HasExports[owner] != 0) code[used++] = 0;
                code[used++] = 0x0b;
            }

            used = RejectUnsupportedName(code, used);
            code[used++] = 0xd0; code[used++] = 0x6d; // no matching definition
            code[used++] = 0x0b;
            sizes[function] = used - offsets[function];
            offsets[function + 1] = used;
            used = WriteMatcher(code, used);
            sizes[function + 1] = used - offsets[function + 1];
            return used;
        }

        private static int CompareEntry(byte[] code, int used, int[] entries, int entry, int match)
        {
            int length = entries[entry + AssemblyTypeLookup.NameLength];
            used = Local(code, used, 0x20, Length);
            used = Const(code, used, length);
            code[used++] = 0x46;
            used = If(code, used);
            used = Local(code, used, 0x20, Input);
            used = Local(code, used, 0x20, Start);
            used = Local(code, used, 0x20, Catalog);
            used = Const(code, used, entries[entry + AssemblyTypeLookup.NameOffset]);
            used = Local(code, used, 0x20, Length);
            code[used++] = 0x10;
            used = WasmBinary.Unsigned(code, used, match);
            used = If(code, used);
            code[used++] = 0x23;
            used = WasmBinary.Unsigned(code, used, entries[entry + AssemblyTypeLookup.Descriptor]);
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            code[used++] = 0x0b;
            return used;
        }

        private static int SkipLeadingSpace(byte[] code, int used)
        {
            used = BeginLoop(code, used);
            used = StopAtLength(code, used, Start, Length);
            used = Character(code, used, Input, Start);
            used = Local(code, used, 0x22, Module); // scratch code unit before module selection
            used = Const(code, used, 32);
            code[used++] = 0x46;
            used = Local(code, used, 0x20, Module);
            used = Const(code, used, 9);
            code[used++] = 0x6b;
            used = Const(code, used, 4);
            code[used++] = 0x4d; // unsigned TAB through CR
            code[used++] = 0x72;
            code[used++] = 0x45;
            code[used++] = 0x0d; code[used++] = 1;
            return Advance(code, used, Start);
        }

        private static int RejectUnsupportedName(byte[] code, int used)
        {
            // Exact known escaped metadata names have already matched.
            // An unresolved composite type expression cannot mean absent.
            used = Local(code, used, 0x20, Start);
            used = Local(code, used, 0x20, Length);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, Length);
            used = BeginLoop(code, used);
            used = StopAtLength(code, used, Start, Length);
            used = Character(code, used, Input, Start);
            used = Local(code, used, 0x21, Module);
            const string syntax = "[],*&\\";
            for (int i = 0; i < syntax.Length; i++)
            {
                used = Local(code, used, 0x20, Module);
                used = Const(code, used, syntax[i]);
                code[used++] = 0x46;
                used = If(code, used);
                code[used++] = 0; // unsupported composite name
                code[used++] = 0x0b;
            }
            return Advance(code, used, Start);
        }

        private static int WriteMatcher(byte[] code, int used)
        {
            const int index = 5;
            code[used++] = 1; code[used++] = 1; code[used++] = 0x7f;
            used = BeginLoop(code, used);
            used = StopAtLength(code, used, index, 4);
            used = ReadAt(code, used, 0, 1, index);
            used = ReadAt(code, used, 2, 3, index);
            code[used++] = 0x47;
            used = If(code, used);
            used = Const(code, used, 0);
            code[used++] = 0x0f;
            code[used++] = 0x0b;
            used = Advance(code, used, index);
            used = Const(code, used, 1);
            code[used++] = 0x0b;
            return used;
        }

        private static int ReadAt(byte[] code, int used, int array, int start, int index)
        {
            used = Local(code, used, 0x20, array);
            used = Local(code, used, 0x20, start);
            used = Local(code, used, 0x20, index);
            code[used++] = 0x6a;
            return ReadChar(code, used);
        }

        private static int Character(byte[] code, int used, int array, int index)
        {
            used = Local(code, used, 0x20, array);
            used = Local(code, used, 0x20, index);
            return ReadChar(code, used);
        }

        private static int ReadChar(byte[] code, int used)
        {
            code[used++] = 0xfb; code[used++] = 0x0d; code[used++] = 4;
            return used;
        }

        private static int BeginLoop(byte[] code, int used)
        {
            code[used++] = 0x02; code[used++] = 0x40;
            code[used++] = 0x03; code[used++] = 0x40;
            return used;
        }

        private static int StopAtLength(byte[] code, int used, int index, int length)
        {
            used = Local(code, used, 0x20, index);
            used = Local(code, used, 0x20, length);
            code[used++] = 0x46;
            code[used++] = 0x0d; code[used++] = 1;
            return used;
        }

        private static int Advance(byte[] code, int used, int index)
        {
            used = Local(code, used, 0x20, index);
            used = Const(code, used, 1);
            code[used++] = 0x6a;
            used = Local(code, used, 0x21, index);
            code[used++] = 0x0c; code[used++] = 0;
            code[used++] = 0x0b; code[used++] = 0x0b;
            return used;
        }

        private static int If(byte[] code, int used)
        {
            code[used++] = 0x04; code[used++] = 0x40;
            return used;
        }
    }
}
