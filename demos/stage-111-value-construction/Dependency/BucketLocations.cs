namespace ValueConstructionDependency
{
    public static class BucketLocations
    {
        public static ref int Take(int[] buckets, int index) => ref buckets[index];
        public static void Add(ref int bucket, int value) => bucket = unchecked(bucket + value);
    }
}
