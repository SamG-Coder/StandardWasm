using PeLibrary;

public static class MethodSpecProbe
{
    public static byte[] Create(int length) => new byte[length];
    public static void Write(byte[] image, int index, int value) => image[index] = unchecked((byte)value);

    public static int[] Inspect(byte[] image, int row)
    {
        var heaps = new int[8];
        var methods = new int[4];
        var counts = new int[45];
        var signature = new int[MethodSpecifications.Width];
        int status = CompressedTables.FindWithHeaps(image, methods, heaps);
        if (status >= 0) status = MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts);
        if (status >= 0) status = MethodSpecifications.Read(image, heaps, methods, counts, row, signature);
        if (status < 0) return new[] { status };
        int first = signature[MethodSpecifications.Arguments], end = signature[MethodSpecifications.End];
        var output = new int[3 + end - first];
        output[1] = signature[MethodSpecifications.Target];
        output[2] = signature[MethodSpecifications.Arity];
        for (int i = first; i < end; i++) output[3 + i - first] = image[i];
        return output;
    }

    public static int ResultLength(int[] result) => result.Length;
    public static int Read(int[] result, int index) => result[index];
}
