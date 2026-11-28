using System;
namespace VirtualTargetFixture
{
    public class Shape { public virtual int Sides(int scale) => 0; public virtual int Corners(int scale) => -1; }
    public class Square : Shape { public override int Sides(int scale) => 4 * scale; }
    public sealed class Triangle : Shape { public override int Sides(int scale) => 3 * scale; }
    public abstract class Animal { public abstract int Legs(int count); }
    public sealed class Dog : Animal { public override int Legs(int count) => 4 * count; }
    public interface IMeasure { int Measure(int value); }
    public sealed class Ruler : IMeasure { public int Measure(int value) => value + 100; }
    public class Widget
    {
        public int Ticks;
        protected virtual void OnTick(object sender, EventArgs args) => Ticks += 1;
        public EventHandler Handler() => OnTick;
    }
    public sealed class Gadget : Widget { protected override void OnTick(object sender, EventArgs args) => Ticks += 10; }
    public class Converter { public virtual T Convert<T>(T value) => value; }

    public static class Kernels
    {
        public static int OverrideThroughBase() { Shape shape = new Square(); Func<int, int> f = shape.Sides; return f(3); } // 12
        public static int SealedOverride() { Shape shape = new Triangle(); Func<int, int> f = shape.Sides; return f(3); } // 9
        public static int NotOverridden() { Shape shape = new Square(); Func<int, int> f = shape.Corners; return f(3); } // -1
        public static int BaseInstance() { Shape shape = new Shape(); Func<int, int> f = shape.Sides; return f(3); } // 0
        public static int AbstractTarget() { Animal a = new Dog(); Func<int, int> f = a.Legs; return f(2); } // 8
        public static int InterfaceTarget() { IMeasure m = new Ruler(); Func<int, int> f = m.Measure; return f(5); } // 105
        public static int VirtualEventHandler()
        {
            var gadget = new Gadget();
            EventHandler h = gadget.Handler();
            EventHandler both = h + gadget.Handler();
            both(null, EventArgs.Empty);
            both -= gadget.Handler();
            both(null, EventArgs.Empty);
            return gadget.Ticks + (h == gadget.Handler() ? 1000 : 0); // 1030
        }
        // One ldvirtftn target bound through two receiver classes in the same module: the shared
        // dispatcher site for Shape.Sides must select each target's own override.
        public static int SharedDispatcherArms()
        {
            Shape square = new Square(), triangle = new Triangle(), plain = new Shape();
            Func<int, int> a = square.Sides, b = triangle.Sides, c = plain.Sides;
            return a(2) + b(2) * 100 + c(2) * 10000; // 608
        }

        // A generic virtual method's ldvirtftn names a MethodSpec: still a located diagnostic.
        public static int RejectGenericVirtual() { var c = new Converter(); Func<int, int> f = c.Convert<int>; return f(5); }

        public static int NullReceiver()
        {
            Shape shape = null;
            try { Func<int, int> f = shape.Sides; return f(1); }
            catch (NullReferenceException) { return 7; }
        }
    }
}
