using PeLibrary;

namespace PortableCompiler
{
    // A module slot is the first field of a runtime Type descriptor. Module
    // and Assembly objects are canonical per slot and keep its identity.
    internal sealed class RuntimeReflectionPlan
    {
        internal const int Module = 0, Assembly = 1, NameOffset = 2, NameLength = 3, Width = 4;
        private const int ModuleRow = -1, AssemblyRow = -2;
        private const string UnknownName = "<Unknown>";

        internal readonly int[] TypeNameFixups = new int[1];
        internal readonly int[] TypeSimpleNameFixups = new int[1];
        internal readonly int[] TypeNamespaceFixups = new int[1];
        internal readonly int[] HeapFixups = new int[1];
        internal readonly RuntimeTypeRelations TypeRelations = new RuntimeTypeRelations();
        internal readonly RuntimeObjectTypes ObjectTypes = new RuntimeObjectTypes();
        internal readonly RuntimeInterfaceTests InterfaceTests = new RuntimeInterfaceTests();
        internal readonly AggregateBoxPlan AggregateBoxes = new AggregateBoxPlan();
        internal int[] Slots { get; private set; }
        internal int ResourceOwner { get; private set; } = -1;
        internal int ResourceOpen { get; private set; }

        internal int[] TypeTextFixups(int form) => form == RuntimeTypeNames.Display ? TypeNameFixups :
            form == RuntimeTypeNames.Simple ? TypeSimpleNameFixups : TypeNamespaceFixups;

        internal int BindResource(BundleImages bundle)
        {
            if (ResourceOpen != 0) return 0;
            var layout = new int[4];
            var heaps = new int[8];
            var methods = new int[4];
            for (int owner = 0; owner < bundle.Count; owner++)
            {
                byte[] image = bundle.Get(owner);
                if (CompressedTables.FindWithHeaps(image, layout, heaps) < 0) return -1;
                int match = ResourceNativeMetadata.Identify(image, heaps, layout, methods);
                if (match < 0) return match;
                if (match == 0) continue;
                if (ResourceOpen != 0) return -2;
                ResourceOwner = owner;
                ResourceOpen = methods[2];
            }
            return ResourceOpen == 0 ? -2 : 0;
        }

        internal int Bind(BundleImages bundle, int[] globals, byte[] literals, int[] literalSize)
        {
            if (Slots != null) return 0;
            var data = new int[(bundle.Count + 1) * Width];
            var layout = new int[4];
            var heaps = new int[8];
            var counts = new int[45];
            int core = 0;
            for (int slot = 0; slot <= bundle.Count; slot++)
            {
                int offset = slot * Width;
                byte[] name = slot == 0 ? null : bundle.FileName(slot - 1);
                if (slot > 0)
                {
                    byte[] image = bundle.Get(slot - 1);
                    if (CompressedTables.FindWithHeaps(image, layout, heaps) >= 0)
                    {
                        for (int i = 0; i < counts.Length; i++) counts[i] = 0;
                        if (MetadataTableRows.ReadCounts(image, heaps[4], heaps[5], counts) >= 0 &&
                            PlatformAssemblyIdentity.IsCoreLibDefinition(image, heaps, layout, counts))
                            core = core == 0 ? slot : -1;
                    }
                }
                int owner = -2 - slot;
                int module = StaticFields.Register(globals, owner, ModuleRow, ObjectLayouts.ObjectReference);
                int assembly = StaticFields.Register(globals, owner, AssemblyRow, ObjectLayouts.ObjectReference);
                if (module < 0 || assembly < 0) return -3;
                data[offset + Module] = module;
                data[offset + Assembly] = assembly;
                int bytes = name == null ? UnknownName.Length * 2 : name.Length;
                int start = literalSize[0];
                if (bytes > literals.Length - start) return -3;
                for (int i = 0; i < bytes; i++)
                    literals[start + i] = name != null ? name[i] :
                        (i & 1) == 0 ? (byte)UnknownName[i / 2] : (byte)0;
                data[offset + NameOffset] = start;
                data[offset + NameLength] = bytes / 2;
                literalSize[0] = start + bytes;
            }
            // System.Object has a reserved symbolic Type even without CoreLib.
            // When CoreLib is present, it shares its actual module and assembly.
            if (core > 0)
                for (int i = 0; i < Width; i++) data[i] = data[core * Width + i];
            Slots = data;
            return 0;
        }

        internal int BodyCapacity(int remaining)
        {
            int slots = Slots.Length / Width;
            return slots > (remaining - 40) / 40 || remaining < 40 ? -3 : 40 + slots * 40;
        }
    }
}
