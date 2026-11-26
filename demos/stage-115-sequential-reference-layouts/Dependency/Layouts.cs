using System.Runtime.InteropServices;

namespace SequentialLayoutDependency
{
    // The browser-wasm CoreLib declares System.Delegate and System.Type this
    // way: a sequential, abstract reference type with default packing.
    [StructLayout(LayoutKind.Sequential)]
    public abstract class Shape
    {
        public int Id;
        public Shape Next;

        protected Shape(int id) { Id = id; }

        public abstract int Area();
        public virtual int Weight() => Id;
    }

    // An auto-layout subclass of a sequential base loads on CoreCLR.
    public sealed class Rectangle : Shape
    {
        public int Width, Height;

        public Rectangle(int id, int width, int height) : base(id)
        {
            Width = width;
            Height = height;
        }

        public override int Area() => Width * Height;
        public override int Weight() => base.Weight() * 10 + Width;
    }

    public struct Pair
    {
        public int Left;
        public int Right;
    }

    // Sequential class fields of every supported storage kind and a static
    // field whose declaring type has no initializer.
    [StructLayout(LayoutKind.Sequential)]
    public class Ledger
    {
        public static int Mark;
        public byte Tag;
        public long Total;
        public Pair Last;
        public Shape Owner;
    }

    [StructLayout(LayoutKind.Sequential)]
    public sealed class Account : Ledger
    {
        public int Extra;
    }

    public class Plain
    {
        public int Seed;
    }

    // CoreCLR throws TypeLoadException when a sequential class derives from an
    // auto class other than Object.
    [StructLayout(LayoutKind.Sequential)]
    public class SequentialFromAuto : Plain
    {
        public int Extra;
    }

    // A precise type initializer is diagnosed for sequential classes as it is
    // for auto classes.
    [StructLayout(LayoutKind.Sequential)]
    public class Initialized
    {
        public static readonly int Base;
        public int Value;

        static Initialized() { Base = 42; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Packed
    {
        public byte Tag;
        public int Value;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public class Sized
    {
        public int Value;
    }

    [ComImport, Guid("00000000-0000-0000-0000-000000000115"), StructLayout(LayoutKind.Sequential)]
    public class ImportedSequential
    {
        public int Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    public class Overlaid
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(0)] public uint Bits;
    }
}
