namespace NativeFrameFieldFixture
{
    public static unsafe class Probe
    {
        private struct Record
        {
#pragma warning disable CS0649 // The oracle checks zero initialization of adjacent fields.
            internal int Stamp;
#pragma warning restore CS0649
            internal fixed char Text[8];
        }

        private struct Envelope
        {
#pragma warning disable CS0649 // The oracle checks zero initialization of adjacent fields.
            internal long Prefix;
#pragma warning restore CS0649
            internal Record Value;
        }

        public static int ReadAfterWrite()
        {
            Record record = default;
            char* first = &record.Text[0];
            first[0] = '\ufedc';
            first[7] = 'Z';
            return record.Text[0] + first[7] + record.Stamp;
        }

        public static int SnapshotAndClear()
        {
            Record record = default;
            char* address = &record.Text[0];
            address[0] = 'A';
            Record snapshot = record;
            record = default;
            return snapshot.Text[0] * 100 + address[0] + record.Text[0];
        }

        public static int NestedOffsets()
        {
            Envelope first = default, second = default;
            char* a = &first.Value.Text[0];
            char* b = &second.Value.Text[0];
            a[0] = 'M';
            b[0] = 'N';
            return a != b && first.Prefix == 0 &&
                first.Value.Text[0] == 'M' && second.Value.Text[0] == 'N' ? 1 : 0;
        }

        public static int AddressOnly()
        {
            Record first = default, second = default;
            return &first.Text[0] != &second.Text[0] ? 1 : 0;
        }

        public static int ManagedByrefAlongsidePointer()
        {
            Record record = default;
            SetStamp(ref record);
            char* address = &record.Text[0];
            address[0] = 'A';
            return record.Stamp + address[0];
        }

        private static void SetStamp(ref Record record) => record.Stamp = 137;
    }
}
