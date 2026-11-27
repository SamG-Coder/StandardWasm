using ValueConstraintDependency;

namespace ValueConstraintDemo
{
    public static class Kernels
    {
        public static int Run(int value)
        {
            var frame = new Frame();
            if (frame.Scalar.Present || frame.Scalar.Value != 0) return -1;
            Cell<int> scalar = default;
            scalar.Present = true;
            scalar.Value = value;
            frame.Scalar = scalar;

            Payload<int> payload = default;
            payload.Value = 9;
            Cell<Payload<int>> aggregate = default;
            aggregate.Value = payload;
            frame.Aggregate = aggregate;
            scalar.Value = value + 1;

            Payload<int> saved = frame.Aggregate.Value;
            NominalCell<NominalValue> nominal = default;
            nominal.Value = saved.Value;
            return !frame.Scalar.Present ? -2 : unchecked(frame.Scalar.Value + scalar.Value + nominal.Value);
        }

        public static int ReadConstraint(ConstrainedReference<int> value) => value == null ? 1 : -1;
        public static int RejectStandaloneConstructor(ConstructorOnly<int> value) => value == null ? 1 : -1;
        public static int RejectNominal(NominalConstraint<NominalDerived> value) => value == null ? 1 : -1;
    }

    public static class Demo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int CompileBundle(byte[] bundle, byte[] entries, byte[] output) =>
            PortableCompiler.MethodCompiler.CompileBundle(bundle, entries, output);
    }
}
