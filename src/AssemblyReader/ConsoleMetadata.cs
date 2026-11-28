using static PeLibrary.ImageBytes;

namespace PeLibrary
{
    // Recognizes the Console output members that the browser host implements.
    // A member is recognized only when its parent TypeRef names System.Console
    // or System.IO.TextWriter in an authentic platform assembly. Any other
    // member of those two types is reported as unsupported, never ignored.
    internal static class ConsoleMetadata
    {
        internal const int Unsupported = -1, StandardOutput = 1, StandardError = 2,
            Write = 3, WriteLine = 4, WriteEmptyLine = 5,
            // static string Console.ReadLine() (Stage 151, decision 0079).
            ReadLine = 6,
            // instance void TextWriter.Flush() on a console writer (decision 0104).
            Flush = 7;

        internal static int Member(byte[] image, int[] streams, int[] methods, int row)
        {
            var counts = new int[45];
            int first = MetadataTableRows.ReadCounts(image, streams[4], streams[5], counts);
            if (first < 0 || row < 1 || row > counts[10]) return 0;
            int table = MetadataTableRows.TableStart(streams, methods, counts, 10);
            if (table < 0) return 0;
            int strings = methods[2], blobs = methods[3];
            int width = StandaloneSignatures.Width(counts, 10, strings, blobs);
            uint at = (uint)(table + (row - 1) * width);
            int parentWidth = width - strings - blobs;
            uint parent = Index(image, at, parentWidth);
            if ((parent & 7) != 1) return 0; // MemberRefParent tag 1 is TypeRef.
            int owner = PlatformTypeReferences.Identify(image, streams, methods, counts, first, (int)(parent >> 3));
            if (owner != PlatformTypeReferences.Console && owner != PlatformTypeReferences.TextWriter) return 0;

            uint name = Index(image, at + (uint)parentWidth, strings);
            uint sig = Index(image, at + (uint)(parentWidth + strings), blobs);
            uint heap = (uint)streams[2], heapSize = (uint)streams[3];
            if (sig == 0 || sig >= (uint)streams[1]) return Unsupported;
            int blobEnd = streams[0] + streams[1];
            var next = new int[1];
            int size = CompressedUnsigned(image, streams[0] + (int)sig, blobEnd, next);
            int signature = next[0];
            if (size < 0 || size > blobEnd - signature) return Unsupported;

            if (owner == PlatformTypeReferences.Console)
            {
                // static string ReadLine(): a line without its terminator, or null at EOF.
                if (PlatformTypeReferences.Named(image, heap, heapSize, name, 8, 0x64616552, 0x656e694c, 0))
                    return size == 3 && image[signature] == 0 && image[signature + 1] == 0 && image[signature + 2] == 0x0e
                        ? ReadLine : Unsupported;
                // static class TextWriter get_Out() / get_Error()
                bool output = PlatformTypeReferences.Named(image, heap, heapSize, name, 7, 0x5f746567, 0x0074754f, 0);
                bool error = PlatformTypeReferences.Named(image, heap, heapSize, name, 9, 0x5f746567, 0x6f727245, 0x72);
                if (!output && !error || size < 4 || image[signature] != 0 || image[signature + 1] != 0 ||
                    image[signature + 2] != 0x12)
                    return Unsupported;
                int coded = CompressedUnsigned(image, signature + 3, signature + size, next);
                if (coded < 0 || next[0] != signature + size || (coded & 3) != 1 ||
                    PlatformTypeReferences.Identify(image, streams, methods, counts, first, coded >> 2) != PlatformTypeReferences.TextWriter)
                    return Unsupported;
                return output ? StandardOutput : StandardError;
            }

            // instance void Flush()
            if (PlatformTypeReferences.Named(image, heap, heapSize, name, 5, 0x73756c46, 0x68, 0))
                return size == 3 && image[signature] == 0x20 && image[signature + 1] == 0 && image[signature + 2] == 1
                    ? Flush : Unsupported;
            // instance void Write(string) / WriteLine(string) / WriteLine()
            bool write = PlatformTypeReferences.Named(image, heap, heapSize, name, 5, 0x74697257, 0x65, 0);
            bool line = PlatformTypeReferences.Named(image, heap, heapSize, name, 9, 0x74697257, 0x6e694c65, 0x65);
            if (!write && !line || size < 3 || image[signature] != 0x20) return Unsupported;
            if (line && size == 3 && image[signature + 1] == 0 && image[signature + 2] == 1)
                return WriteEmptyLine;
            if (size == 4 && image[signature + 1] == 1 && image[signature + 2] == 1 && image[signature + 3] == 0x0e)
                return write ? Write : WriteLine;
            return Unsupported;
        }

        private static uint Index(byte[] image, uint at, int width) => width == 2 ? U16(image, at) : U32(image, at);
    }
}
