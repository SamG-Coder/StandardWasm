using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BootstrapLibrary;
using PlatformIdentityProbe;

internal static class ValueBaseCases
{
    internal static void Add(List<object> cases)
    {
        const int capacity = 16384;
        Type entry = typeof(ValueBaseProbe);
        Type enumType = typeof(EnumReference), valueType = typeof(ValueReference);
        Assembly thirdParty = enumType.BaseType!.Assembly;
        if (enumType.IsValueType || valueType.IsValueType || enumType.BaseType.FullName != "System.Enum" ||
            valueType.BaseType!.FullName != "System.ValueType" || thirdParty != valueType.BaseType.Assembly ||
            thirdParty == typeof(Enum).Assembly)
            throw new InvalidOperationException("The extern-alias fixtures must be CLR reference types with third-party bases.");

        byte[] image = File.ReadAllBytes(entry.Assembly.Location);
        byte[] dependency = File.ReadAllBytes(thirdParty.Location);
        byte[] bundle = BundleCases.Pack(image, dependency);
        byte[] output = new byte[capacity];
        int[] location = CompilerExports.CreateDiagnostic();
        using var pe = new PEReader(new MemoryStream(image));
        var metadata = pe.GetMetadataReader();
        int metadataStart = pe.PEHeaders.MetadataStartOffset;

        Reject(nameof(ValueBaseProbe.EnumLayout), typeof(EnumCarrier), enumType, isEnum: true);
        Reject(nameof(ValueBaseProbe.ValueLayout), typeof(ValueCarrier), valueType, isEnum: false);
        Check(nameof(ValueBaseProbe.AliasEnum), ValueBaseProbe.AliasEnum);
        Check(nameof(ValueBaseProbe.AliasValueType), ValueBaseProbe.AliasValueType);

        void Reject(string name, Type carrier, Type reference, bool isEnum)
        {
            // Start with an ordinary CLR class reference. Force only the layout
            // request to use a value signature, and give the impostor the
            // corresponding storage shape. The third-party base must still
            // prevent the compiler from registering it as a CLI value type.
            var carrierField = (FieldDefinitionHandle)MetadataTokens.Handle(carrier.GetField("Item")!.MetadataToken);
            var definition = metadata.GetFieldDefinition(carrierField);
            var signature = metadata.GetBlobReader(definition.Signature);
            if (signature.ReadByte() != 6 || signature.ReadByte() != 0x12 ||
                signature.ReadTypeHandle() != MetadataTokens.Handle(reference.MetadataToken) || signature.RemainingBytes != 0)
                throw new InvalidOperationException("Expected a field with the extern-alias reference type.");

            byte[] forged = (byte[])image.Clone();
            int heap = metadataStart + metadata.GetHeapMetadataOffset(HeapIndex.Blob);
            int blob = heap + MetadataTokens.GetHeapOffset(definition.Signature);
            if (forged[blob] >= 0x80)
                throw new InvalidOperationException("Expected a short field signature.");
            forged[blob + 2] = 0x11; // ELEMENT_TYPE_VALUETYPE

            if (isEnum)
            {
                int fieldToken = reference.GetField(nameof(EnumReference.value__))!.MetadataToken;
                int offset = RowOffset(TableIndex.Field, fieldToken);
                ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(forged.AsSpan(offset));
                // An enum backing field has both SpecialName and RTSpecialName.
                BinaryPrimitives.WriteUInt16LittleEndian(forged.AsSpan(offset), (ushort)(flags | 0x0600));
            }
            else
            {
                int offset = RowOffset(TableIndex.TypeDef, reference.MetadataToken);
                uint flags = BinaryPrimitives.ReadUInt32LittleEndian(forged.AsSpan(offset));
                BinaryPrimitives.WriteUInt32LittleEndian(forged.AsSpan(offset), (flags & ~0x18u) | 0x08u);
            }

            int row = entry.GetMethod(name)!.MetadataToken & 0xffffff;
            byte[] manifest = Manifest(row);
            byte[] input = BundleCases.Pack(forged, dependency);
            int[] diagnostic = [0, row, -1];
            Array.Fill(output, (byte)0xa5);
            int status = CompilerExports.CompileBundleDetailed(input, manifest, output, location);
            if (status != -2 || !location.SequenceEqual(diagnostic) || output.Any(value => value != 0xa5))
                throw new InvalidOperationException($"{name}: status {status}, location [{string.Join(", ", location)}].");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "third-party value base: " + name,
                inputs = new[] { Encode(input), Encode(manifest) },
                generated = new { capacity, bundle = true, arguments = Array.Empty<int>(), diagnostic, reject = true },
                expected = status
            });
        }

        void Check(string name, Func<int, int> clr)
        {
            byte[] manifest = Manifest(entry.GetMethod(name)!.MetadataToken & 0xffffff);
            int size = CompilerExports.CompileBundleDetailed(bundle, manifest, output, location);
            if (size <= 0)
                throw new InvalidOperationException($"{name}: status {size}, location [{string.Join(", ", location)}].");
            int value = clr(-241);
            if (value != -241)
                throw new InvalidOperationException($"{name}: the CLR reference did not alias.");
            cases.Add(new
            {
                module = "BootstrapLibrary", method = "CompileBundle", fixture = "third-party value base: " + name,
                inputs = new[] { Encode(bundle), Encode(manifest) },
                generated = new { capacity, bundle = true, arguments = new[] { -241 },
                    hex = Convert.ToHexString(output.AsSpan(0, size)).ToLowerInvariant() },
                expected = value
            });
        }

        int RowOffset(TableIndex table, int token) =>
            metadataStart + metadata.GetTableMetadataOffset(table) +
            ((token & 0xffffff) - 1) * metadata.GetTableRowSize(table);
    }

    private static byte[] Manifest(int row)
    {
        byte[] manifest = new byte[11];
        BinaryPrimitives.WriteInt32LittleEndian(manifest, row);
        manifest[4] = 3;
        "run"u8.CopyTo(manifest.AsSpan(8));
        return manifest;
    }

    private static object Encode(byte[] bytes) => new { bytes = Array.ConvertAll(bytes, value => (int)value) };
}
