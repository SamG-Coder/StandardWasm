using PeLibrary;
using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class RuntimeReflectionWasm
    {
        internal static int Read(byte[] code, int used, int bodyStart, int operation,
            int receiver, int result, int scratch, RuntimeReflectionPlan plan)
        {
            used = Local(code, used, 0x20, receiver);
            code[used++] = 0xfb;
            code[used++] = 22; // ref.cast to the deferred descriptor heap
            used = WasmIndexFixups.Write(code, used, bodyStart, plan.HeapFixups);
            code[used++] = 0xfb;
            code[used++] = 2; // struct.get module slot
            used = WasmIndexFixups.Write(code, used, bodyStart, plan.HeapFixups);
            code[used++] = 0;
            used = Local(code, used, 0x21, scratch);

            code[used++] = 0x02; // block
            code[used++] = 0x40;
            int[] slots = plan.Slots;
            for (int offset = 0; offset < slots.Length; offset += RuntimeReflectionPlan.Width)
            {
                used = Local(code, used, 0x20, scratch);
                used = Const(code, used, offset / RuntimeReflectionPlan.Width);
                code[used++] = 0x46; // i32.eq
                code[used++] = 0x04; // if
                code[used++] = 0x40;
                if (operation == ReflectionMembers.ModuleName)
                {
                    used = Const(code, used, slots[offset + RuntimeReflectionPlan.NameOffset]);
                    used = Const(code, used, slots[offset + RuntimeReflectionPlan.NameLength]);
                    code[used++] = 0xfb;
                    code[used++] = 9; // array.new_data Char[], passive literal section
                    code[used++] = 4;
                    code[used++] = 0;
                    used = StringWasm.Wrap(code, used);
                }
                else
                {
                    int field = operation == ReflectionMembers.TypeModule ? RuntimeReflectionPlan.Module : RuntimeReflectionPlan.Assembly;
                    code[used++] = 0x23;
                    used = WasmBinary.Unsigned(code, used, slots[offset + field]);
                }
                used = Local(code, used, 0x21, result);
                code[used++] = 0x0c; // br out of the lookup block
                code[used++] = 1;
                code[used++] = 0x0b; // end if
            }
            code[used++] = 0x00; // Only linker-created descriptors can reach here.
            code[used++] = 0x0b; // end block
            return used;
        }

    }
}
