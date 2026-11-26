using System;

namespace DelegateLibrary
{
    public static class DelegateDemo
    {
        public static byte[] CreateBytes(int length) => new byte[length];
        public static void SetByte(byte[] image, int index, int value) => image[index] = unchecked((byte)value);
        public static int ReadByte(byte[] image, int index) => image[index];
        public static int FindRow(byte[] image, byte[] name) => PeLibrary.MethodNames.FindRow(image, name);
        public static int Compile(byte[] image, int row, byte[] output) =>
            PortableCompiler.MethodCompiler.Compile(image, row, output);
    }

    public sealed class Receiver
    {
        private int value;
        public Receiver(int seed) => value = seed;
        public int Add() => value = unchecked(value + 1);
        public int Subtract() => value = unchecked(value - 2);
        public int OtherArity(int step) => value + step;
    }

    public static class Kernels
    {
        public static int Route(int input, int branch)
        {
            var first = new Receiver(input);
            var second = new Receiver(20);
            Func<int> binding;
            if (branch >= 0)
                binding = first.Add;
            else
                binding = second.Subtract;
            binding();
            return binding();
        }

        public static Func<bool> UnsupportedSignature(int value) => new Flag(value).Read;
    }

    public sealed class Flag
    {
        private int value;
        public Flag(int seed) => value = seed;
        public bool Read() => value != 0;
    }
}
