#nullable enable

using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using SwitchLibrary;

internal static class PortableSwitchCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 32768;
        byte[] image = File.ReadAllBytes(typeof(Dispatch).Assembly.Location);
        var encoded = Encode(image);
        var output = new byte[capacity];
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();

        string classify = nameof(Dispatch.Classify);
        string walk = nameof(Dispatch.Walk);
        string addChoice = nameof(Dispatch.AddChoice);
        foreach (string fixture in new[] { classify, walk, addChoice })
        {
            int row = Row(fixture);
            int rva = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row)).RelativeVirtualAddress;
            byte[] il = pe.GetMethodBody(rva).GetILBytes()!;
            int table = IlOffsets.Find(il, OpCodes.Switch);
            if (table < 0) throw new InvalidOperationException($"{fixture} no longer emits IL switch");

            string hex = Compile(image, row, fixture);
            int body = MethodOffset(pe.PEHeaders, rva);
            int header = (image[body] & 3) == 2 ? 1 : (image[body + 1] >> 4) * 4;
            int count = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(table + 1));
            int tableEnd = table + 5 + count * 4;
            int[] inputs = [int.MinValue, -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, int.MaxValue];
            foreach (int input in inputs)
            {
                if (fixture == classify)
                    Record([input], Dispatch.Classify(input));
                else if (fixture == walk)
                    foreach (int steps in new[] { -1, 0, 1, 2, 7, 33 }) Record([input, steps], Dispatch.Walk(input, steps));
                else
                    foreach (long seed in new[] { long.MinValue, 9007199254740993L, long.MaxValue })
                        Record([Wide(seed), input], Wide(Dispatch.AddChoice(seed, input)));
            }

            if (fixture == classify)
            {
                int[] targets = Enumerable.Range(0, count)
                    .Select(index => BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(table + 5 + index * 4)))
                    .ToArray();
                if (targets.Distinct().Count() == count)
                    throw new InvalidOperationException("The classification fixture lost its shared switch target.");

                Reject("table target inside the switch operand", table + 5, table + 1 - tableEnd);
                Reject("table target beyond the method", table + 5, int.MaxValue);
                Reject("oversized switch count", table + 1, -1);

                void Reject(string name, int offset, int value)
                {
                    byte[] changed = (byte[])image.Clone();
                    BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(body + header + offset), value);
                    Array.Fill(output, (byte)0xa5);
                    int status = CompilerExports.Compile(changed, row, output);
                    if (status != -1 || output.Any(item => item != 0xa5))
                        throw new InvalidOperationException($"{name}: compiler returned {status} or modified output");
                    cases.Add(new
                    {
                        module = "BootstrapLibrary", method = "Compile", fixture = name, inputs = new object[] { Encode(changed), row },
                        generated = new { capacity, arguments = Array.Empty<int>(), reject = true }, expected = -1
                    });
                }
            }

            if (fixture == walk)
            {
                // Retarget one entry to the earlier jump to the loop condition.
                // It has the same empty stack as the original case entry.
                const int loopEntryJump = 3;
                if (table <= loopEntryJump || il[loopEntryJump] != 0x2b)
                    throw new InvalidOperationException("The loop fixture lost its entry jump.");
                byte[] changed = (byte[])image.Clone();
                BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(body + header + table + 5 + 2 * 4),
                    loopEntryJump - tableEnd);
                var oracle = Load<Func<int, int, int>>(changed, fixture);
                const string name = "backward switch target";
                string modifiedHex = Compile(changed, row, name);
                object modified = Encode(changed);
                foreach (int value in new[] { 0, 2, 3 })
                    RecordWithImage(name, modified, modifiedHex, [value, 7], oracle(value, 7));
            }
            if (fixture == addChoice)
            {
                // Keep the seed on the evaluation stack through every case in
                // place of Roslyn's spill and reload. Both DLLs run on the CLR.
                if (header == 1 || il[0] != 0x02 || il[1] != 0x0a || il[^4] != 0x06)
                    throw new InvalidOperationException("The wide fixture lost its seed local.");
                byte[] changed = (byte[])image.Clone();
                changed[body + header + 1] = 0; // nop instead of stloc.0
                changed[body + header + il.Length - 4] = 0; // nop instead of ldloc.0
                int stack = BinaryPrimitives.ReadUInt16LittleEndian(changed.AsSpan(body + 2));
                BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(body + 2), (ushort)(stack + 1));
                var oracle = Load<Func<long, int, long>>(changed, fixture);
                const string name = "i64 value live across switch";
                string modifiedHex = Compile(changed, row, name);
                object modified = Encode(changed);
                const long seed = 9007199254740993L;
                foreach (int value in inputs)
                {
                    long expected = oracle(seed, value);
                    if (expected != Dispatch.AddChoice(seed, value))
                        throw new InvalidOperationException("The IL rewrite changed the CLR result.");
                    RecordWithImage(name, modified, modifiedHex, [Wide(seed), value], Wide(expected));
                }
            }

            void Record(object[] args, object expected) => RecordWithImage(fixture, encoded, hex, args, expected);

            void RecordWithImage(string name, object file, string expectedHex, object[] args, object expected) =>
                cases.Add(new
                {
                    module = "BootstrapLibrary", method = "Compile", fixture = name, inputs = new object[] { file, row },
                    generated = new { capacity, arguments = args, hex = expectedHex }, expected
                });
        }

        string Compile(byte[] file, int row, string fixture)
        {
            int size = CompilerExports.Compile(file, row, output);
            if (size <= 0) throw new InvalidOperationException($"{fixture}: portable compiler returned {size}");
            return Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant();
        }
    }

    private static T Load<T>(byte[] image, string method) where T : Delegate =>
        Assembly.Load(image).GetType(typeof(Dispatch).FullName!, throwOnError: true)!.GetMethod(method)!.CreateDelegate<T>();

    private static int Row(string method) => typeof(Dispatch).GetMethod(method)!.MetadataToken & 0xffffff;

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };

    private static object Wide(long value) => new { i64 = value.ToString(CultureInfo.InvariantCulture) };

    private static int MethodOffset(PEHeaders headers, int rva)
    {
        int sectionIndex = headers.GetContainingSectionIndex(rva);
        var section = headers.SectionHeaders[sectionIndex];
        return section.PointerToRawData + rva - section.VirtualAddress;
    }
}
