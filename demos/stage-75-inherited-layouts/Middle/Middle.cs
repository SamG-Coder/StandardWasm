using HierarchyBase;

namespace HierarchyMiddle
{
    public class Middle : Root
    {
        public int Adjusted;

        protected Middle(int value) : base(unchecked(value + 4))
        {
            Adjusted = unchecked(value + 7);
        }
    }
}
