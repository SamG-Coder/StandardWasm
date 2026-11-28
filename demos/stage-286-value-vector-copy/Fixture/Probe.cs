using System;

namespace ValueVectorCopy
{
    public sealed class Marker { }

    public struct Item
    {
        public int Number;
        public Marker Reference;

        public Item(int number, Marker reference)
        {
            Number = number;
            Reference = reference;
        }
    }

    public static class Probe
    {
        private static Item[] Sequence() =>
            new[] { new Item(1, null), new Item(2, null), new Item(3, null), new Item(4, null) };

        public static int ExactCopyAndReferencePayload()
        {
            var marker = new Marker();
            var source = new[] { new Item(3, marker), new Item(7, null) };
            var destination = new Item[2];
            Array.Copy(source, destination, 2);
            return object.ReferenceEquals(marker, destination[0].Reference) &&
                destination[0].Number == 3 && destination[1].Number == 7 ? 101 : -1;
        }

        public static int RightOverlap()
        {
            var array = Sequence();
            Array.Copy(array, 0, array, 1, 3);
            return array[0].Number == 1 && array[1].Number == 1 &&
                array[2].Number == 2 && array[3].Number == 3 ? 103 : -1;
        }

        public static int LeftOverlap()
        {
            var array = Sequence();
            Array.Copy(array, 1, array, 0, 3);
            return array[0].Number == 2 && array[1].Number == 3 &&
                array[2].Number == 4 && array[3].Number == 4 ? 107 : -1;
        }

        public static int DestinationAlias()
        {
            var source = new[] { new Item(3, null) };
            var destination = new[] { new Item(5, null) };
            ref Item alias = ref destination[0];
            Array.Copy(source, destination, 1);
            if (alias.Number != 3) return -1;
            alias = new Item(17, null);
            return destination[0].Number == 17 && source[0].Number == 3 ? 109 : -1;
        }

        public static int IndependentSlots()
        {
            var source = new[] { new Item(3, null) };
            var destination = new Item[1];
            Array.Copy(source, destination, 1);
            source[0] = new Item(5, null);
            if (destination[0].Number != 3) return -1;
            destination[0] = new Item(7, null);
            return source[0].Number == 5 ? 113 : -1;
        }

        public static int DefaultSnapshots()
        {
            var source = new Item[2];
            var destination = new[] { new Item(3, new Marker()), default(Item) };
            ref Item alias = ref destination[0];
            Array.Copy(source, destination, 2);
            return alias.Number == 0 && alias.Reference == null &&
                destination[1].Number == 0 ? 127 : -1;
        }

        public static int EmptyCopy()
        {
            var array = Sequence();
            Array.Copy(array, 4, array, 4, 0);
            return array[0].Number == 1 && array[3].Number == 4 ? 131 : -1;
        }

        public static int TypedNullBeforeLength()
        {
            var array = Sequence();
            try { Array.Copy(null, array, -1); }
            catch (ArgumentNullException) { return 137; }
            return -1;
        }

        public static int TypedNegativeLength()
        {
            var array = Sequence();
            try { Array.Copy(array, array, -1); }
            catch (ArgumentOutOfRangeException) { return 139; }
            return -1;
        }

        public static int TypedSourceRangeBeforeDestinationIndex()
        {
            var array = Sequence();
            try { Array.Copy(array, 3, array, -1, 2); }
            catch (ArgumentOutOfRangeException) { return array[0].Number == 1 ? 147 : -1; }
            catch (ArgumentException) { return array[0].Number == 1 ? 149 : -1; }
            return -1;
        }

        public static int NoWriteOnDestinationRangeFailure()
        {
            var array = Sequence();
            try { Array.Copy(array, 0, array, 3, 2); }
            catch (ArgumentException) { return array[3].Number == 4 ? 151 : -1; }
            return -1;
        }

        public static int PrimitiveRegression()
        {
            var array = new[] { 1, 2, 3 };
            Array.Copy(array, 0, array, 1, 2);
            return array[1] == 1 && array[2] == 2 ? 157 : -1;
        }
    }
}
