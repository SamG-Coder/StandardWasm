namespace PortableCompiler
{
    // Stage 156 (decision 0083): the native imports a program reaches. Each entry is one Wasm
    // function import with its own function type, written after every fixed import group, so the
    // fixed groups' function indices never move. Discovery adds entries while compiling; the
    // program is then recompiled with the grown table reserved (MethodCompiler.CompileProgram),
    // so within one pass import index k is FunctionCount(features) + k.
    //   P/Invoke (and CoreLib QCall):  module "native:<ModuleRef name>", name = ImplMap entry point
    //   InternalCall:                  module "internalcall", name = "Namespace.Type::Method"
    //   Host helpers:                  module "standardwasm:native"
    //   Routed native indirect calls:  module "standardwasm:com", name = the host signature
    // Self-hosting: flat arrays and one byte pool, no jagged arrays or ref helpers.
    internal sealed class NativeImportTable
    {
        // Helper entries, keyed by negative rows of owner -1.
        internal const int PresentHelper = 1, ClearErrorHelper = 2, SaveErrorHelper = 3, LibraryHelper = 4;

        private int[] owners = new int[8], rows = new int[8];
        private int[] moduleStarts = new int[8], moduleLengths = new int[8], nameStarts = new int[8], nameLengths = new int[8];
        private int[] typeStarts = new int[8], parameterCounts = new int[8], results = new int[8];
        private byte[] pool = new byte[256];
        private int poolUsed;

        internal int Count;

        internal int Find(int owner, int row)
        {
            for (int i = 0; i < Count; i++)
                if (owners[i] == owner && rows[i] == row) return i;
            return -1;
        }

        // types holds the parameters' Wasm value types; result is a Wasm value type or 0 for none.
        internal int Add(int owner, int row, byte[] module, int moduleLength, byte[] name, int nameLength,
            byte[] types, int parameters, int result)
        {
            int existing = Find(owner, row);
            if (existing >= 0) return existing;
            if (Count == owners.Length)
            {
                owners = Grow(owners); rows = Grow(rows);
                moduleStarts = Grow(moduleStarts); moduleLengths = Grow(moduleLengths);
                nameStarts = Grow(nameStarts); nameLengths = Grow(nameLengths);
                typeStarts = Grow(typeStarts); parameterCounts = Grow(parameterCounts); results = Grow(results);
            }
            int index = Count;
            owners[index] = owner;
            rows[index] = row;
            moduleStarts[index] = Append(module, moduleLength);
            moduleLengths[index] = moduleLength;
            nameStarts[index] = Append(name, nameLength);
            nameLengths[index] = nameLength;
            typeStarts[index] = Append(types, parameters);
            parameterCounts[index] = parameters;
            results[index] = result;
            Count = index + 1;
            return index;
        }

        //   present(i32 entry) -> i32   1 when the host implements entry, else 0
        //   library(i32 entry) -> i32   1 when entry's library is available (its entry may still
        //                               be missing), else 0: DllNotFoundException on the CLR
        //   clearLastError()            the CLR's SetLastSystemError(0) before a SetLastError call
        //   saveLastError()             the CLR's SetLastPInvokeError(GetLastSystemError()) after it
        internal int Helper(int which)
        {
            int existing = Find(-1, -which);
            if (existing >= 0) return existing;
            byte[] module = Ascii("standardwasm:native");
            string text = which == PresentHelper ? "present" : which == LibraryHelper ? "library" :
                which == ClearErrorHelper ? "clearLastError" : "saveLastError";
            byte[] name = Ascii(text);
            var types = new byte[1];
            types[0] = 0x7f;
            bool query = which == PresentHelper || which == LibraryHelper; // (i32) -> i32
            return Add(-1, -which, module, module.Length, name, name.Length, types, query ? 1 : 0, query ? 0x7f : 0);
        }

        internal int TypeBytes()
        {
            int size = 0;
            for (int i = 0; i < Count; i++)
                size += 1 + WasmBinary.UnsignedSize(parameterCounts[i]) + parameterCounts[i] + 1 + (results[i] != 0 ? 1 : 0);
            return size;
        }

        internal int WriteTypes(byte[] output, int cursor)
        {
            for (int i = 0; i < Count; i++)
            {
                output[cursor++] = 0x60;
                cursor = WasmBinary.Unsigned(output, cursor, parameterCounts[i]);
                for (int p = 0; p < parameterCounts[i]; p++) output[cursor++] = pool[typeStarts[i] + p];
                if (results[i] == 0) output[cursor++] = 0;
                else { output[cursor++] = 1; output[cursor++] = (byte)results[i]; }
            }
            return cursor;
        }

        internal int ImportEntryBytes(int firstType)
        {
            int size = 0;
            for (int i = 0; i < Count; i++)
                size += WasmBinary.UnsignedSize(moduleLengths[i]) + moduleLengths[i] +
                    WasmBinary.UnsignedSize(nameLengths[i]) + nameLengths[i] + 1 + WasmBinary.UnsignedSize(firstType + i);
            return size;
        }

        internal int WriteEntries(byte[] output, int cursor, int firstType)
        {
            for (int i = 0; i < Count; i++)
            {
                cursor = WasmBinary.Unsigned(output, cursor, moduleLengths[i]);
                for (int b = 0; b < moduleLengths[i]; b++) output[cursor++] = pool[moduleStarts[i] + b];
                cursor = WasmBinary.Unsigned(output, cursor, nameLengths[i]);
                for (int b = 0; b < nameLengths[i]; b++) output[cursor++] = pool[nameStarts[i] + b];
                output[cursor++] = 0; // function import
                cursor = WasmBinary.Unsigned(output, cursor, firstType + i);
            }
            return cursor;
        }

        // The entry's module and name, for the EntryPointNotFoundException message.
        internal int ModuleByte(int entry, int i) => pool[moduleStarts[entry] + i];
        internal int ModuleLength(int entry) => moduleLengths[entry];
        internal int NameByte(int entry, int i) => pool[nameStarts[entry] + i];
        internal int NameLength(int entry) => nameLengths[entry];

        internal static byte[] Ascii(string text)
        {
            var bytes = new byte[text.Length];
            for (int i = 0; i < text.Length; i++) bytes[i] = (byte)text[i];
            return bytes;
        }

        private int Append(byte[] bytes, int length)
        {
            while (poolUsed + length > pool.Length)
            {
                var larger = new byte[pool.Length * 2];
                for (int i = 0; i < poolUsed; i++) larger[i] = pool[i];
                pool = larger;
            }
            int start = poolUsed;
            for (int i = 0; i < length; i++) pool[poolUsed++] = bytes[i];
            return start;
        }

        private static int[] Grow(int[] values)
        {
            var larger = new int[values.Length * 2];
            for (int i = 0; i < values.Length; i++) larger[i] = values[i];
            return larger;
        }
    }
}
