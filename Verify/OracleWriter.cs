using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

internal static class OracleWriter
{
    internal static void Write(string path, List<object> cases)
    {
        var blobs = new BlobIndexConverter();
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(type =>
        {
            // Only fixture binary arguments use the single-property { bytes = int[] } shape.
            if (type.Kind == JsonTypeInfoKind.Object && type.Properties.Count == 1 &&
                type.Properties[0].Name == "bytes" && type.Properties[0].PropertyType == typeof(int[]))
                type.Properties[0].CustomConverter = blobs;
        });

        using var output = File.Create(path);
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();
        writer.WritePropertyName("cases");
        JsonSerializer.Serialize(writer, cases, new JsonSerializerOptions { TypeInfoResolver = resolver });
        writer.WritePropertyName("blobs");
        JsonSerializer.Serialize(writer, blobs.Payloads);
        writer.WriteEndObject();
    }

    private sealed class BlobIndexConverter : JsonConverter<int[]>
    {
        private readonly Dictionary<int[], int> indices = new(new PayloadComparer());
        internal List<int[]> Payloads { get; } = [];

        public override int[] Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, int[] value, JsonSerializerOptions options)
        {
            if (!indices.TryGetValue(value, out int index))
            {
                index = Payloads.Count;
                indices.Add(value, index);
                Payloads.Add(value);
            }
            writer.WriteNumberValue(index);
        }
    }

    private sealed class PayloadComparer : IEqualityComparer<int[]>
    {
        public bool Equals(int[] left, int[] right) => left.AsSpan().SequenceEqual(right);

        public int GetHashCode(int[] values)
        {
            var hash = new HashCode();
            hash.AddBytes(MemoryMarshal.AsBytes(values.AsSpan()));
            return hash.ToHashCode();
        }
    }
}
