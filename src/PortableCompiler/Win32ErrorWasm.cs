using PeLibrary;

namespace PortableCompiler
{
    // The Windows SDK's generated P/Invoke wrapper explicitly transfers
    // the platform error into .NET's separate cached P/Invoke slot.
    internal static class Win32ErrorWasm
    {
        internal const int FunctionCount = 4, TypeBytes = FunctionCount * 4;
        private const string Module = "standardwasm:win32";

        private static string Name(int member) =>
            member == MarshalErrorMetadata.GetSystem ? "getLastSystemError" :
            member == MarshalErrorMetadata.SetSystem ? "setLastSystemError" :
            member == MarshalErrorMetadata.GetPInvoke ? "getLastPInvokeError" : "setLastPInvokeError";

        internal static int ImportEntryBytes(int firstType)
        {
            int bytes = 0;
            for (int member = 1; member <= FunctionCount; member++)
                bytes += Module.Length + Name(member).Length + 3 + WasmBinary.UnsignedSize(firstType + member - 1);
            return bytes;
        }

        internal static int WriteTypes(byte[] output, int cursor)
        {
            for (int member = 1; member <= FunctionCount; member++)
            {
                output[cursor++] = 0x60;
                if (MarshalErrorMetadata.IsSetter(member))
                {
                    output[cursor++] = 1;
                    output[cursor++] = 0x7f;
                    output[cursor++] = 0;
                }
                else
                {
                    output[cursor++] = 0;
                    output[cursor++] = 1;
                    output[cursor++] = 0x7f;
                }
            }
            return cursor;
        }

        internal static int WriteEntries(byte[] output, int cursor, int firstType)
        {
            for (int member = 1; member <= FunctionCount; member++)
            {
                cursor = WriteName(output, cursor, Module);
                cursor = WriteName(output, cursor, Name(member));
                output[cursor++] = 0; // function import
                cursor = WasmBinary.Unsigned(output, cursor, firstType + member - 1);
            }
            return cursor;
        }

        private static int WriteName(byte[] output, int cursor, string name)
        {
            output[cursor++] = (byte)name.Length;
            for (int i = 0; i < name.Length; i++) output[cursor++] = (byte)name[i];
            return cursor;
        }
    }
}
