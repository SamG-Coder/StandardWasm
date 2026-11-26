using PeLibrary;
using static PortableCompiler.SignatureTypes;

namespace PortableCompiler
{
    internal static class ResourceOpenIl
    {
        internal const int Locals = 3;

        // The metadata binding proves this override's signature and exact
        // prelude. The platform call consumes its two arguments, and writes
        // the pointer and out length to the original frame locations.
        internal static int Frame(int[] frame, int parameters)
        {
            if (parameters != 2 || frame[0] != RuntimeTypeMetadata.Assembly || frame[1] != String) return -2;
            if (frame.Length - parameters < Locals) return -3;
            frame[parameters] = RuntimeTypeMetadata.Assembly;
            frame[parameters + 1] = Native;
            frame[parameters + 2] = I32;
            return Locals;
        }

        internal static int Decode(byte[] image, int[] body, int[] records, int[] location, int parameters)
        {
            records[0] = BranchIl.ResourceLookup;
            records[1] = parameters + 1; // pointer local
            records[2] = ResourceStreamMetadata.AfterNativeSetup;
            records[3] = parameters + 2; // out uint local
            return BranchIl.Decode(image, body[0], body[1], parameters, Locals, records, location,
                false, ResourceStreamMetadata.AfterNativeSetup);
        }
    }
}
