using PeLibrary;

namespace PortableCompiler
{
    // Stage 156 (decision 0083): a reached method without IL that the host implements. A P/Invoke
    // (PinvokeImpl with an ImplMap row, including CoreLib QCalls) or an InternalCall compiles to a
    // thunk with synthetic, pre-linked records:
    //
    //   0      NativePresent       [] -> [i32]   present(entry): does the host implement it?
    //   1      brtrue 10           [i32] -> []
    //   2      NativePresent       [] -> [i32]   library(entry): is its library available?
    //   3      brtrue 7            [i32] -> []
    //   4..6   ldstr message; newobj DllNotFoundException(string); throw
    //   7..9   ldstr message; newobj EntryPointNotFoundException(string); throw
    //   10..   ldarg 0 .. n-1      [] -> [args]
    //          NativeHelper        clearLastError       (SetLastError = true only)
    //          CallNative          [args] -> [result]   the typed import
    //          NativeHelper        saveLastError        (SetLastError = true only)
    //          ret
    //
    // The CLR binds lazily and raises at the call: DllNotFoundException when the library cannot be
    // loaded, EntryPointNotFoundException when it loads but lacks the entry. The host answers
    // present and library from what it implements, so the module raises the same CoreLib
    // exception with the CLR's message, and the same catch clause runs as on the CLR.
    // Only blittable scalar and unmanaged pointer signatures are lowered (see Blittable); anything
    // else is a located -2.
    internal static class NativeThunks
    {
        internal const int None = 0, PlatformInvoke = 1, InternalCall = 2, SymbolLookup = 3;
        private const int StaticMethod = 0x10, PinvokeImpl = 0x2000, PreserveSig = 0x80, InternalCallFlag = 0x1000;
        private const int SupportsLastError = 0x40;

        internal static int Kind(byte[] image, int[] heaps, int[] layout, int row)
        {
            if ((MethodDefinitions.ReadColumn(image, layout, row, 2) & PinvokeImpl) != 0)
                return IsSymbolLookup(image, heaps, layout, row) ? SymbolLookup : PlatformInvoke;
            if ((MethodDefinitions.ReadColumn(image, layout, row, 1) & InternalCallFlag) != 0) return InternalCall;
            return None;
        }

        private static bool IsSymbolLookup(byte[] image, int[] heaps, int[] layout, int row)
        {
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob <= 0 || blob > heaps[1] - 8) return false;
            int at = heaps[0] + blob;
            if (ImageBytes.U32(image, (uint)at) != 0x18030007 ||
                ImageBytes.U32(image, (uint)(at + 4)) != 0x08070f18) return false;
            if (MethodDefinitions.ReadColumn(image, layout, row, 1) != PreserveSig) return false;
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
            return first >= 0 && PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts) &&
                NativeImports.Find(image, heaps, layout, counts, "QCall", "NativeLibrary_GetSymbol", row, 0x101) == row;
        }

        // The MethodDef row of `instance void .ctor(string)` on TypeDef row type, or 0.
        internal static int FindMessageConstructor(byte[] image, int[] heaps, int[] layout, int type)
        {
            var range = new int[2];
            if (ObjectLayouts.MethodRange(image, heaps, layout, type, range) < 0) return 0;
            for (int method = range[0]; method < range[1]; method++)
            {
                if (!ImageBytes.MatchesAscii(image, heaps, (uint)MethodDefinitions.ReadColumn(image, layout, method, 3), ".ctor")) continue;
                int blob = MethodDefinitions.ReadColumn(image, layout, method, 4);
                if (blob <= 0 || blob >= heaps[1]) continue;
                int at = heaps[0] + blob;
                if (image[at] == 4 && image[at + 1] == 0x20 && image[at + 2] == 1 && image[at + 3] == 1 && image[at + 4] == 0x0e)
                    return method;
            }
            return 0;
        }

        // Records for n parameters: 10 for the presence checks, then the forwarding call.
        internal static int RecordCount(int parameters, bool setLastError, int kind) =>
            10 + parameters + 2 + (kind == SymbolLookup ? 8 : setLastError ? 2 : 0);

        // Describes the import for row: module and name (UTF-8) into module/name, the parameters'
        // Wasm value types into types, and the result type (0 for void) in shape[1]. shape[0] gets
        // the parameter count and shape[2] the SetLastError flag. Returns the lengths packed as
        // moduleLength | nameLength << 16, or a negative status.
        internal static int Describe(byte[] image, int[] heaps, int[] layout, int row, int kind,
            byte[] module, byte[] name, byte[] types, int[] shape)
        {
            int attributes = MethodDefinitions.ReadColumn(image, layout, row, 2);
            int implementation = MethodDefinitions.ReadColumn(image, layout, row, 1);
            // Instance InternalCalls and HRESULT-translating (non-PreserveSig) imports have
            // marshalling this step does not perform.
            if ((attributes & StaticMethod) == 0) return -2;
            if (kind == SymbolLookup)
            {
                for (int i = 0; i < 3; i++) types[i] = 0x7f;
                shape[0] = 3; shape[1] = 0x7f; shape[2] = 0;
                return Put(module, 0, "native:QCall") | Put(name, 0, "NativeLibrary_GetSymbol") << 16;
            }
            int status = Blittable(image, heaps, layout, row, types, shape);
            if (status < 0) return status;
            int moduleLength, nameLength;
            if (kind == PlatformInvoke)
            {
                if ((implementation & PreserveSig) == 0) return -2;
                var counts = new int[45];
                if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) < 0) return -1;
                var map = new int[3];
                int found = NativeImports.ForMethod(image, heaps, layout, counts, row, map);
                if (found <= 0) return found < 0 ? -1 : -2;
                shape[2] = (map[0] & SupportsLastError) != 0 ? 1 : 0;
                moduleLength = Put(module, 0, "native:");
                moduleLength = CopyString(image, heaps, map[2], module, moduleLength);
                nameLength = CopyString(image, heaps, map[1], name, 0);
            }
            else
            {
                shape[2] = 0;
                moduleLength = Put(module, 0, "internalcall");
                var names = new int[2];
                int type = ObjectLayouts.Owner(image, heaps, layout, row, false);
                if (type < 1 || ObjectLayouts.TypeDefinitionNames(image, heaps, layout, type, names) < 0) return -1;
                nameLength = 0;
                if (names[1] != 0)
                {
                    int named = CopyString(image, heaps, names[1], name, 0);
                    if (named < 0) return named;
                    if (named != 0) nameLength = Put(name, named, ".");
                }
                nameLength = CopyString(image, heaps, names[0], name, nameLength);
                if (nameLength < 0) return nameLength;
                nameLength = Put(name, nameLength, "::");
                nameLength = CopyString(image, heaps, MethodDefinitions.ReadColumn(image, layout, row, 3), name, nameLength);
            }
            if (moduleLength < 0) return moduleLength;
            if (nameLength < 0) return nameLength;
            return moduleLength | nameLength << 16;
        }

        // ECMA-335 II.23.2.1 MethodDefSig of a static method: default calling convention, then
        // blittable scalar element types and unmanaged pointers (Stage 365) only. bool (4-byte Win32
        // BOOL) and char (ANSI by default) need marshalling, so they are rejected rather than passed
        // through as integers; so are byrefs, strings, arrays, delegates and by-value structs.
        private static int Blittable(byte[] image, int[] heaps, int[] layout, int row, byte[] types, int[] shape)
        {
            int blob = MethodDefinitions.ReadColumn(image, layout, row, 4);
            if (blob <= 0 || blob >= heaps[1]) return -1;
            int at = heaps[0] + blob;
            int length = image[at++];
            if (length >= 0x80 || length < 3) return length >= 0x80 ? -2 : -1;
            int end = at + length;
            if (image[at++] != 0) return -2; // generic, varargs or instance
            int count = image[at++];
            if (count >= 0x80 || count > types.Length) return -2;
            var cursor = new int[] { at };
            int result = SignatureType(image, end, cursor, true);
            if (result < 0) return result;
            for (int i = 0; i < count; i++)
            {
                int type = SignatureType(image, end, cursor, false);
                if (type < 0) return type;
                if (type == 0) return -2;
                types[i] = (byte)type;
            }
            if (cursor[0] != end) return -2;
            shape[0] = count;
            shape[1] = result;
            return 0;
        }

        // Stage 365 (decision 0083, step 2): one parameter or result type at cursor[0] within
        // [.., end), advanced past it. A scalar maps through WasmType. An unmanaged pointer
        // (ELEMENT_TYPE_PTR) becomes a wasm32 i32 address into memory 0, passed unchanged. The CLR
        // never marshals a T* either: it hands the callee the address the caller computed, so no
        // copy or pin is involved. Only the callee's view of the pointee must agree with ours,
        // so the pointee needs a layout the compiler reproduces (see Pointee). Returns the Wasm
        // value type, 0 for a void result, -2 for a type this step does not lower, or -1 for a
        // malformed signature.
        private static int SignatureType(byte[] image, int end, int[] cursor, bool result)
        {
            if (cursor[0] >= end) return -1;
            int element = image[cursor[0]++];
            if (element != 0x0f)
            {
                int scalar = WasmType(element, result);
                return scalar < 0 ? -2 : scalar;
            }
            int pointee = Pointee(image, end, cursor, 0);
            return pointee < 0 ? pointee : 0x7f;
        }

        // The type after an ELEMENT_TYPE_PTR. Admitted: void, the integer and floating-point
        // primitives, IntPtr/UIntPtr, another pointer, and a value type defined in the same
        // assembly whose sequential layout NativeValueLayouts measures exactly as the x86 CLR
        // does (Stage 143). bool* and char* stay -2: Win32 declarations usually mean a 4-byte BOOL*
        // or a wide string there, and nothing needs them yet. Custom modifiers, function pointers,
        // generic instances, value types from other assemblies, and Pack/Size or auto-layout
        // structs are -2 too.
        private static int Pointee(byte[] image, int end, int[] cursor, int depth)
        {
            if (cursor[0] >= end) return -1;
            if (depth > 8) return -2;
            int element = image[cursor[0]++];
            if (element == 0x01) return 0;                                       // void*
            if (element == 0x0f) return Pointee(image, end, cursor, depth + 1);  // T**
            if (element == 0x11)                                                 // VALUETYPE TypeDefOrRef
            {
                var read = new int[1];
                int coded = ImageBytes.CompressedUnsigned(image, cursor[0], end, read);
                if (coded < 0) return -1;
                cursor[0] = read[0];
                if ((coded & 3) != 0) return -2; // a TypeRef or TypeSpec
                var measured = new int[3];
                int status = NativeValueLayouts.Measure(BundleImages.Single(image), 0, coded >> 2, measured);
                return status == -1 ? -1 : status < 0 ? -2 : 0;
            }
            return WasmType(element, false) > 0 ? 0 : -2;
        }

        // Element type to Wasm value type: 0 for a void result, -1 when not blittable.
        private static int WasmType(int element, bool result)
        {
            if (element == 0x01) return result ? 0 : -1;               // void
            if (element >= 0x04 && element <= 0x09) return 0x7f;     // I1 U1 I2 U2 I4 U4
            if (element == 0x0a || element == 0x0b) return 0x7e;     // I8 U8
            if (element == 0x0c) return 0x7d;                        // R4
            if (element == 0x0d) return 0x7c;                        // R8
            if (element == 0x18 || element == 0x19) return 0x7f;     // I U: 32-bit on wasm32
            return -1;
        }

        // The CLR 10.0.10 (Windows) message of DllNotFoundException for a library that cannot be
        // found: "Unable to load DLL '<dll>' or one of its dependencies: The specified module could
        // not be found. (0x8007007E)", as UTF-8. Returns the byte count.
        internal static int LibraryMessage(byte[] module, int moduleLength, int kind, byte[] message)
        {
            int used = Put(message, 0, "Unable to load DLL '");
            for (int i = kind == PlatformInvoke ? 7 : 0; i < moduleLength; i++) message[used++] = module[i];
            return Put(message, used, "' or one of its dependencies: The specified module could not be found. (0x8007007E)");
        }

        // "Unable to find an entry point named '<name>' in DLL '<dll>'." as UTF-8, where dll is
        // the declared module name (without the "native:" prefix). Returns the byte count.
        internal static int Message(byte[] module, int moduleLength, int kind, byte[] name, int nameLength, byte[] message)
        {
            if (kind == SymbolLookup) return Put(message, 0, "The requested native export was not found.");
            int used = Put(message, 0, "Unable to find an entry point named '");
            for (int i = 0; i < nameLength; i++) message[used++] = name[i];
            used = Put(message, used, "' in DLL '");
            for (int i = kind == PlatformInvoke ? 7 : 0; i < moduleLength; i++) message[used++] = module[i];
            return Put(message, used, "'.");
        }

        // Writes the thunk records. presentImport, clearImport and saveImport are absolute Wasm
        // function indices (clear and save are -1 without SetLastError); callImport likewise.
        // missing[0..2] are the DllNotFoundException message start, length and constructor;
        // missing[3..5] the same for EntryPointNotFoundException.
        internal static void Records(int[] records, int self, int parameters, int entry, int presentImport,
            int libraryImport, int callImport, int clearImport, int saveImport, int[] missing, int kind)
        {
            int pc = 0;
            pc = Put(records, pc, BranchIl.NativePresent, presentImport, entry);
            pc = Put(records, pc, BranchIl.Conditional, 10, 0); // brtrue: callable
            pc = Put(records, pc, BranchIl.NativePresent, libraryImport, entry);
            pc = Put(records, pc, BranchIl.Conditional, 7, 0);  // brtrue: library loads, entry missing
            pc = Put(records, pc, BranchIl.LoadNativeMessage, missing[0], missing[1]);
            pc = Put(records, pc, BranchIl.NewObject, missing[2], 0);
            pc = Put(records, pc, BranchIl.ThrowNull, 0, 0);
            pc = Put(records, pc, BranchIl.LoadNativeMessage, missing[3], missing[4]);
            pc = Put(records, pc, BranchIl.NewObject, missing[5], 0);
            pc = Put(records, pc, BranchIl.ThrowNull, 0, 0);
            for (int i = 0; i < parameters; i++) pc = Put(records, pc, BranchIl.Load, i, 0);
            if (clearImport >= 0) pc = Put(records, pc, BranchIl.NativeHelper, clearImport, 0);
            pc = Put(records, pc, BranchIl.CallNative, self, callImport);
            if (kind == SymbolLookup)
            {
                // The QCall's third argument alone decides whether a missing
                // export returns zero or throws a managed exception.
                pc = Put(records, pc, BranchIl.Dup, 0, 0);
                pc = Put(records, pc, BranchIl.Conditional, pc + 7, 0);
                pc = Put(records, pc, BranchIl.Load, 2, 0);
                pc = Put(records, pc, BranchIl.Conditional, pc + 5, 0x45);
                pc = Put(records, pc, BranchIl.Pop, 0, 0);
                pc = Put(records, pc, BranchIl.LoadNativeMessage, missing[3], missing[4]);
                pc = Put(records, pc, BranchIl.NewObject, missing[5], 0);
                pc = Put(records, pc, BranchIl.ThrowNull, 0, 0);
            }
            else if (saveImport >= 0) pc = Put(records, pc, BranchIl.NativeHelper, saveImport, 0);
            Put(records, pc, BranchIl.Return, 0, 0);
        }

        private static int Put(int[] records, int pc, int kind, int operand, int extra)
        {
            int slot = pc * BranchIl.Width;
            records[slot] = kind;
            records[slot + 1] = operand;
            records[slot + 2] = pc + 1;
            records[slot + 3] = extra;
            return pc + 1;
        }

        private static int Put(byte[] bytes, int used, string text)
        {
            for (int i = 0; i < text.Length; i++) bytes[used++] = (byte)text[i];
            return used;
        }

        // Appends a #Strings entry (UTF-8, bounds-checked, NUL-terminated within the heap).
        private static int CopyString(byte[] image, int[] heaps, int index, byte[] bytes, int used)
        {
            if (index < 0 || index >= heaps[3]) return -1;
            int at = heaps[2] + index, limit = heaps[2] + heaps[3];
            while (at < limit && image[at] != 0)
            {
                if (used == bytes.Length) return -2; // longer than any supported entry name
                bytes[used++] = image[at++];
            }
            return at == limit ? -1 : used;
        }
    }
}
