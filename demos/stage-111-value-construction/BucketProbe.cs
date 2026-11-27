using System;
using ValueConstructionDependency;

namespace ValueConstructionDemo
{
    public static class BucketProbe
    {
        public static int ReadManagedBuckets(int value)
        {
            int[] buckets = new int[3];
            buckets[1] = 19;
            ref int bucket = ref BucketLocations.Take(buckets, 1);
            ref int second = ref BucketLocations.Take(buckets, 1);
            if (value < 0) second = ref BucketLocations.Take(buckets, 2);
            int[] oldBuckets = buckets;
            buckets = new int[3];
            buckets[1] = 3;
            BucketLocations.Add(ref second, value);
            return unchecked(bucket * 7 + oldBuckets[1] * 3 + oldBuckets[2] * 11 + buckets[1]);
        }

        public static int CheckBucketFailure(int index)
        {
            int[] buckets = null;
            if (index != int.MinValue) buckets = new int[1];
            try { return BucketLocations.Take(buckets, index); }
            catch (NullReferenceException) { return -7; }
            catch (IndexOutOfRangeException) { return -11; }
        }

        public static ref int RejectExportedBucket(int[] buckets) => ref BucketLocations.Take(buckets, 0);
    }
}
