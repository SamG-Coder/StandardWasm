using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using MathIntrinsics;

// #141: writes a copy of the stage 29 MathIntrinsics.dll whose platform AssemblyRefs carry a non-empty Culture
// (name, version, flags and public key token unchanged). Such a reference names a satellite-style assembly that is
// not the BCL, and the CLR fails to bind it, so the host compiler must not treat its types as CLI primitives or its
// System.Math as an intrinsic. npm test compiles the copy and expects a diagnostic.
internal static class CultureProbe
{
    internal static void Write(string path)
    {
        byte[] image = File.ReadAllBytes(typeof(Measures).Assembly.Location);
        byte[] patched = (byte[])image.Clone();
        using (var pe = new PEReader(new MemoryStream(image)))
        {
            var reader = pe.GetMetadataReader();
            int metadata = pe.PEHeaders.MetadataStartOffset;
            int strings = reader.GetHeapSize(HeapIndex.String) < 65536 ? 2 : 4;
            int blobs = reader.GetHeapSize(HeapIndex.Blob) < 65536 ? 2 : 4;
            int rowSize = reader.GetTableRowSize(TableIndex.AssemblyRef);
            if (rowSize != 12 + 2 * strings + 2 * blobs)
                throw new InvalidOperationException("Unexpected AssemblyRef row layout");
            // Any non-empty #Strings entry makes the reference non-neutral. The module name is always present. The CLR
            // rejects it as an invalid culture (FileLoadException); a valid one such as "fr" gives FileNotFoundException.
            int culture = MetadataTokens.GetHeapOffset(reader.GetModuleDefinition().Name);
            int patchedRows = 0;
            foreach (var handle in reader.AssemblyReferences)
            {
                if (!IsPlatformName(reader.GetString(reader.GetAssemblyReference(handle).Name))) continue;
                int row = metadata + reader.GetTableMetadataOffset(TableIndex.AssemblyRef) +
                    (MetadataTokens.GetRowNumber(handle) - 1) * rowSize;
                int column = row + 12 + blobs + strings; // version, flags, PublicKeyOrToken, Name, then Culture
                if (strings == 2) BitConverter.TryWriteBytes(patched.AsSpan(column, 2), (ushort)culture);
                else BitConverter.TryWriteBytes(patched.AsSpan(column, 4), culture);
                patchedRows++;
            }
            if (patchedRows == 0) throw new InvalidOperationException("No platform AssemblyRef to patch");
        }
        using (var check = new PEReader(new MemoryStream(patched)))
        {
            var reader = check.GetMetadataReader();
            foreach (var handle in reader.AssemblyReferences)
            {
                var reference = reader.GetAssemblyReference(handle);
                if (IsPlatformName(reader.GetString(reference.Name)) && reference.Culture.IsNil)
                    throw new InvalidOperationException("Culture patch did not apply");
            }
        }
        RequireBindFailure(patched);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, patched);
    }

    // The premise of the fixture: the CLR itself refuses to bind the non-neutral reference when the code runs.
    private static void RequireBindFailure(byte[] patched)
    {
        var context = new AssemblyLoadContext("culture-probe", isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(new MemoryStream(patched));
            assembly.GetType("MathIntrinsics.Measures", throwOnError: true)!.GetMethod("Sqrt")!.Invoke(null, [4.0]);
        }
        catch (Exception e) when (e is FileNotFoundException or FileLoadException or TypeLoadException ||
            e.InnerException is FileNotFoundException or FileLoadException or TypeLoadException)
        {
            return;
        }
        finally { context.Unload(); }
        throw new InvalidOperationException("The CLR bound a culture-specific platform reference");
    }

    private static bool IsPlatformName(string name) => name is "netstandard" or "mscorlib" or "System.Runtime";
}
