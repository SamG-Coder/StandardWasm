using System;
using ValueConstructionDependency;

namespace ValueConstructionDemo
{
    public static class ByRefReturnProbe
    {
        public static int CheckReturnedLocations(int value)
        {
            var values = new StoredValue[2];
            var entries = new Entries<StoredValue>(values);
            ref StoredValue first = ref entries.At(0);
            ref StoredValue alias = ref entries.At(0);
            first.Number = value;
            StoredValue snapshot = values[0];
            alias.Number = unchecked(value + 17);
            entries.At(1).Number = 23;

            var objects = new object[1];
            ref object reference = ref ReturnedLocations.At(objects, 0);
            object original = new object();
            reference = original;
            int identity = objects[0] == original ? 29 : 0;
            ref object objectAlias = ref ReturnedLocations.At(objects, 0);
            objectAlias = null;
            if (reference == null) identity += 31;
            return unchecked(first.Number * 7 + snapshot.Number * 3 + values[1].Number + identity);
        }

        public static int CheckReturnedLocationFailure(int index)
        {
            var entries = new Entries<StoredValue>(new StoredValue[1]);
            try { return entries.At(index).Number; }
            catch (IndexOutOfRangeException) { return -11; }
        }

        public static ref StoredValue RejectExportedLocation(StoredValue[] values) =>
            ref new Entries<StoredValue>(values).At(0);
    }
}
