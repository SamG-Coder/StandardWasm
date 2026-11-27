using static PortableCompiler.BranchWasm;

namespace PortableCompiler
{
    internal static class MonoAssemblyIdentityWasm
    {
        internal static int Write(byte[] code, int used, MonoAssemblyIdentityData plan, int[] bodies, int[] sizes)
        {
            if (plan.Function < 0) return used;
            int remaining = code.Length - used;
            if (remaining < 128 || plan.RelocationCount > (remaining - 128) / 35) return -3;
            const int slot = 0, region = 1, relocation = 2;
            bodies[plan.Function] = used;
            code[used++] = 1; code[used++] = 2; code[used++] = 0x7f;
            used = Local(code, used, 0x20, slot);
            used = Const(code, used, 1);
            code[used++] = 0x6b;
            used = Local(code, used, 0x22, slot);
            used = Const(code, used, plan.Modules);
            code[used++] = 0x4f; // reject slots outside the verified bundle
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0; code[used++] = 0x0b;

            used = Global(code, used, 0x23, plan.Cache);
            used = Local(code, used, 0x22, region);
            code[used++] = 0x45;
            code[used++] = 0x04; code[used++] = 0x40;
            int pages = ((plan.DataSize - 1) >> 16) + 1;
            used = Const(code, used, pages);
            code[used++] = 0x40; code[used++] = 0; // memory.grow
            used = Local(code, used, 0x22, region);
            used = Const(code, used, 0);
            code[used++] = 0x4c; // host memory starts with page zero reserved
            code[used++] = 0x04; code[used++] = 0x40;
            code[used++] = 0; code[used++] = 0x0b;
            used = Local(code, used, 0x20, region);
            used = Const(code, used, 16);
            code[used++] = 0x74;
            used = Local(code, used, 0x22, region);
            used = Const(code, used, plan.DataStart);
            used = Const(code, used, plan.DataSize);
            code[used++] = 0xfc; code[used++] = 8; code[used++] = 0; code[used++] = 0; // memory.init
            for (int index = 0; index < plan.RelocationCount; index++)
            {
                used = Local(code, used, 0x20, region);
                used = Const(code, used, plan.Relocations[index]);
                code[used++] = 0x6a;
                used = Local(code, used, 0x22, relocation);
                used = Local(code, used, 0x20, relocation);
                code[used++] = 0x28; code[used++] = 2; code[used++] = 0;
                used = Local(code, used, 0x20, region);
                code[used++] = 0x6a;
                code[used++] = 0x36; code[used++] = 2; code[used++] = 0;
            }
            used = Local(code, used, 0x20, region);
            used = Global(code, used, 0x24, plan.Cache);
            code[used++] = 0x0b;

            used = Local(code, used, 0x20, region);
            used = Local(code, used, 0x20, slot);
            used = Const(code, used, MonoAssemblyIdentityData.RecordSize);
            code[used++] = 0x6c;
            code[used++] = 0x6a;
            code[used++] = 0x0b;
            sizes[plan.Function] = used - bodies[plan.Function];
            return used;
        }

        private static int Global(byte[] code, int used, byte operation, int index)
        {
            code[used++] = operation;
            return WasmBinary.Unsigned(code, used, index);
        }
    }
}
