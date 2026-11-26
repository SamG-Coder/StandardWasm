namespace StructCopyFixture
{
    // Stage 145 (decision 0073): whole blittable structs copied between memory 0 and the
    // managed aggregate (ldobj/stobj/initobj through a native address). The x86 CLR runs
    // the same DLL over Marshal.AllocHGlobal memory as the oracle.
    public struct Rect { public int Left, Top, Right, Bottom; }
    // No padding: ECMA-335 leaves padding bytes unspecified. The CLR copies them with the
    // struct, while a GC aggregate has none, so the memory images compare padding-free
    // layouts (decision 0073).
    public struct Inner { public int Tag; public int Spare; public long Value; }
    public struct Outer { public int S; public int Tail; public Inner In; }

    // A byte-backed enum field is a 1-byte scalar in memory (Codex review of #296).
    public enum Marker : byte { None, Alpha = 7, Beta = 200 }
    public struct Tagged { public Marker Kind; public byte Low; public short Mid; public int Count; }

    public static unsafe class Kernels
    {
        public static int CopyIn(Rect* p)
        {
            Rect r = *p;
            return unchecked(r.Right - r.Left + r.Bottom * 3 + r.Top);
        }

        public static int CopyOut(Rect* p, int v)
        {
            var r = new Rect { Left = v, Top = v + 1, Right = v + 2, Bottom = v + 3 };
            *p = r;
            return p->Bottom;
        }

        public static int DefaultOut(Rect* p)
        {
            *p = default;
            return p->Left + p->Bottom;
        }

        public static int Swap(Rect* a, Rect* b)
        {
            Rect t = *a;
            *a = *b;
            *b = t;
            return unchecked(a->Left * 1000 + b->Left);
        }

        public static long NestedCopy(Outer* p)
        {
            Outer o = *p;
            o.Tail = 5;
            // Replace the nested value whole: writing o.In.Tag in place would need a managed
            // byref into the local (#271), which is outside this native-memory stage.
            var inner = new Inner { Tag = 9, Value = o.In.Value };
            o.In = inner;
            *p = o;
            return o.In.Value + o.In.Tag + o.S + p->Tail;
        }

        public static int CopyArray(Rect* source, Rect* destination, int count)
        {
            for (int i = 0; i < count; i++) destination[i] = source[i];
            int sum = 0;
            for (int i = 0; i < count; i++) sum = unchecked(sum * 7 + destination[i].Top);
            return sum;
        }

        public static int EnumCopyIn(Tagged* p)
        {
            Tagged t = *p;
            return (int)t.Kind * 100000 + t.Low * 1000 + t.Count % 1000;
        }

        public static int EnumCopyOut(Tagged* p)
        {
            var t = new Tagged { Kind = Marker.Beta, Low = 3, Mid = -2, Count = 42 };
            *p = t;
            return (int)p->Kind + p->Count + p->Mid;
        }

        // The struct-pointer field path (Stage 143) with an enum field.
        public static int EnumField(Tagged* p)
        {
            p->Kind = Marker.Alpha;
            return (int)p->Kind * 10 + p->Low;
        }

        public static int NullCopy()
        {
            try
            {
                Rect r = *(Rect*)null;
                return r.Left;
            }
            catch (System.NullReferenceException) { return -3; }
        }
    }
}
